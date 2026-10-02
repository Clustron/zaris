using SagaOutbox.Infrastructure;

namespace SagaOutbox.Outbox;

/// <summary>A record as it lives on the shared bus (one topic = one append-only log document).</summary>
public sealed record BusRecord(
    long Seq,
    string EventId,
    string Type,
    string Source,
    string Payload,
    DateTimeOffset AppendedAt);

/// <summary>The stored shape of a topic: a monotonic sequence counter + the append-only record list.</summary>
public sealed class BusLog
{
    public long NextSeq { get; set; } = 1;
    public List<BusRecord> Records { get; set; } = new();
}

/// <summary>
/// A tiny message bus implemented as a Zaris document per topic: an append-only log that the relay
/// writes to and consumers read from. The append is a CAS'd read-modify-write, so concurrent relays
/// never lose or overwrite each other's records.
///
/// The bus is deliberately <b>at-least-once</b>: it does not dedupe on append. If the relay crashes
/// after appending but before marking the source event dispatched, that event is appended again next
/// cycle (a duplicate record with the same <see cref="BusRecord.EventId"/> at a later seq). Removing
/// the <i>effect</i> of such duplicates is the consumer's job — see <see cref="BusConsumer"/>.
/// </summary>
public sealed class EventBus
{
    private readonly ZarisDocumentStore _store;

    public EventBus(ZarisDocumentStore store) => _store = store;

    private static string TopicKey(string topic) => $"bus:topic:{topic}";

    /// <summary>Appends one record and returns its assigned sequence number.</summary>
    public async Task<long> AppendAsync(
        string topic, string eventId, string type, string source, string payload, CancellationToken ct = default)
    {
        long assignedSeq = 0;
        await _store.MutateAsync<BusLog>(
            TopicKey(topic),
            log =>
            {
                assignedSeq = log.NextSeq;
                log.Records.Add(new BusRecord(assignedSeq, eventId, type, source, payload, DateTimeOffset.UtcNow));
                log.NextSeq = assignedSeq + 1;
                return log;
            },
            create: () => new BusLog(),
            ct: ct).ConfigureAwait(false);
        return assignedSeq;
    }

    /// <summary>Returns every record whose seq is strictly greater than <paramref name="afterSeq"/>.</summary>
    public async Task<IReadOnlyList<BusRecord>> ReadAsync(string topic, long afterSeq, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<BusLog>(TopicKey(topic), ct).ConfigureAwait(false);
        if (!doc.Found || doc.Value is null) return Array.Empty<BusRecord>();
        return doc.Value.Records.Where(r => r.Seq > afterSeq).OrderBy(r => r.Seq).ToList();
    }

    /// <summary>Total records on the topic (diagnostics / assertions).</summary>
    public async Task<int> CountAsync(string topic, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<BusLog>(TopicKey(topic), ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.Records.Count : 0;
    }
}
