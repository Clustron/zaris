using System.Text.Json.Nodes;
using SagaOutbox.Infrastructure;

namespace SagaOutbox.Outbox;

/// <summary>Simulated process crash inside the relay, used by tests/demo to exercise the at-least-once window.</summary>
public sealed class RelayCrashException : Exception
{
    public string EventId { get; }
    public RelayCrashException(string eventId) : base($"relay crashed after publishing event {eventId}, before marking it dispatched")
        => EventId = eventId;
}

/// <summary>
/// Moves events from aggregates' embedded outboxes onto the shared <see cref="EventBus"/>.
///
/// For each aggregate document it: (1) reads the document, (2) publishes every <c>dispatched=false</c>
/// event to the bus, then (3) flips those events to <c>dispatched=true</c> with a CAS write. The
/// publish and the flag-flip are <b>deliberately not atomic</b> — a crash between them (see
/// <see cref="CrashAfterPublishing"/>) leaves the event still <c>dispatched=false</c>, so the next
/// cycle publishes it again. That is at-least-once delivery; consumers make it effectively-once.
///
/// It round-trips each document as a generic <see cref="JsonObject"/>, so it works for any aggregate
/// shape without the relay knowing the concrete type, and — crucially — it writes the <i>whole</i>
/// document back, never clobbering business fields when it flips an outbox flag.
/// </summary>
public sealed class OutboxRelay
{
    private readonly ZarisDocumentStore _store;
    private readonly EventBus _bus;
    private readonly string _topic;

    /// <summary>Test/demo hook: if set and it returns true for a just-published event id, the relay
    /// throws <see cref="RelayCrashException"/> before marking anything dispatched.</summary>
    public Func<string, bool>? CrashAfterPublishing { get; set; }

    public OutboxRelay(ZarisDocumentStore store, EventBus bus, string topic)
    {
        _store = store;
        _bus = bus;
        _topic = topic;
    }

    /// <summary>Drains one aggregate document. Returns the number of events delivered to the bus.</summary>
    public async Task<int> PumpKeyAsync(string aggregateKey, string source, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<JsonObject>(aggregateKey, ct).ConfigureAwait(false);
        if (!doc.Found || doc.Value is null) return 0;
        if (doc.Value["outbox"] is not JsonArray outbox) return 0;

        var pending = new List<(string Id, string Type, string Payload)>();
        foreach (var node in outbox)
        {
            if (node is not JsonObject ev) continue;
            if (ev["dispatched"]?.GetValue<bool>() == true) continue;
            pending.Add((
                ev["eventId"]!.GetValue<string>(),
                ev["type"]!.GetValue<string>(),
                ev["payload"]!.GetValue<string>()));
        }
        if (pending.Count == 0) return 0;

        foreach (var p in pending)
        {
            await _bus.AppendAsync(_topic, p.Id, p.Type, source, p.Payload, ct).ConfigureAwait(false);
            if (CrashAfterPublishing?.Invoke(p.Id) == true)
                throw new RelayCrashException(p.Id);
        }

        var ids = pending.Select(p => p.Id).ToHashSet();
        await _store.MutateAsync<JsonObject>(
            aggregateKey,
            n => MarkDispatched(n, ids),
            create: () => throw new ZarisStoreException($"aggregate '{aggregateKey}' vanished while relaying"),
            ct: ct).ConfigureAwait(false);

        return pending.Count;
    }

    /// <summary>Drains every aggregate key in a registry. Returns total events delivered.</summary>
    public async Task<int> PumpAsync(OutboxRegistry registry, string source, CancellationToken ct = default)
    {
        var total = 0;
        foreach (var key in await registry.KeysAsync(ct).ConfigureAwait(false))
            total += await PumpKeyAsync(key, source, ct).ConfigureAwait(false);
        return total;
    }

    private static JsonObject? MarkDispatched(JsonObject node, HashSet<string> ids)
    {
        if (node["outbox"] is not JsonArray outbox) return null;
        var changed = false;
        foreach (var child in outbox)
        {
            if (child is not JsonObject ev) continue;
            if (ids.Contains(ev["eventId"]!.GetValue<string>()) && ev["dispatched"]?.GetValue<bool>() != true)
            {
                ev["dispatched"] = true;
                changed = true;
            }
        }
        return changed ? node : null;
    }
}
