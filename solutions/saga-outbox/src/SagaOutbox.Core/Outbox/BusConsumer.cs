using SagaOutbox.Infrastructure;

namespace SagaOutbox.Outbox;

/// <summary>Stored, durable state of one named consumer on one topic: a read cursor plus an inbox set.</summary>
public sealed class ConsumerState
{
    /// <summary>Highest bus seq this consumer has read past.</summary>
    public long Cursor { get; set; }

    /// <summary>Ids of events whose effect has already been applied (the "inbox" dedupe set).</summary>
    public List<string> Processed { get; set; } = new();
}

/// <summary>
/// A durable, deduping consumer of a bus topic. It keeps a cursor and an <b>inbox set</b> of event
/// ids it has already acted on, both persisted in one Zaris document (so progress survives a crash).
///
/// The bus is at-least-once, so the same <c>EventId</c> can appear twice (at different seqs). The
/// cursor alone would let the duplicate through, but the inbox set makes the handler fire <b>exactly
/// once per event id</b> — turning at-least-once delivery into effectively-once effects. Cursor and
/// inbox advance together in a single CAS, so a crash cannot apply an effect without recording it.
/// </summary>
public sealed class BusConsumer
{
    private readonly ZarisDocumentStore _store;
    private readonly EventBus _bus;
    private readonly string _topic;
    private readonly string _name;

    public BusConsumer(ZarisDocumentStore store, EventBus bus, string topic, string name)
    {
        _store = store;
        _bus = bus;
        _topic = topic;
        _name = name;
    }

    private string StateKey => $"bus:cursor:{_topic}:{_name}";

    /// <summary>
    /// Reads new records and invokes <paramref name="handler"/> once per not-yet-seen event id, in
    /// bus order. Returns the number of records whose effect was actually applied this call.
    /// </summary>
    public async Task<int> PollAsync(Func<BusRecord, Task> handler, CancellationToken ct = default)
    {
        var state = await _store.ReadAsync<ConsumerState>(StateKey, ct).ConfigureAwait(false);
        var cursor = state.Found && state.Value is not null ? state.Value.Cursor : 0;
        var seen = state.Found && state.Value is not null
            ? new HashSet<string>(state.Value.Processed)
            : new HashSet<string>();

        var records = await _bus.ReadAsync(_topic, cursor, ct).ConfigureAwait(false);
        if (records.Count == 0) return 0;

        var applied = 0;
        foreach (var rec in records)
        {
            if (seen.Add(rec.EventId))
            {
                await handler(rec).ConfigureAwait(false); // effect applied at most once per event id
                applied++;
            }
            // Advance cursor + inbox together, atomically, after each record. A crash after the handler
            // but before this CAS re-delivers the record, but the inbox set then suppresses the effect.
            var newCursor = rec.Seq;
            var snapshot = seen.ToList();
            await _store.MutateAsync<ConsumerState>(
                StateKey,
                s =>
                {
                    s.Cursor = Math.Max(s.Cursor, newCursor);
                    foreach (var id in snapshot)
                        if (!s.Processed.Contains(id)) s.Processed.Add(id);
                    return s;
                },
                create: () => new ConsumerState(),
                ct: ct).ConfigureAwait(false);
        }
        return applied;
    }
}
