using System.Text.Json;
using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;

namespace SagaOutbox.Services;

/// <summary>
/// Base class for every service's aggregate root. It bundles the three things that must be written
/// together, in one Zaris document, under one CAS:
/// <list type="number">
///   <item>business state (in the derived class),</item>
///   <item>an <b>idempotency ledger</b> of command ids already applied (so a redelivered or retried
///         command is a no-op — the foundation of saga step idempotency and crash-safe retries),</item>
///   <item>an <b>embedded outbox</b> of domain events (so state and events can never be half-written
///         relative to each other — the dual-write problem is gone).</item>
/// </list>
/// Aggregates are plain mutable classes: <see cref="ZarisDocumentStore.MutateAsync{T}"/> hands the
/// lambda a freshly deserialized instance on every attempt, so mutating it in place is safe.
/// </summary>
public abstract class OutboxAggregate
{
    /// <summary>Monotonic per-aggregate event sequence.</summary>
    public long OutboxSeq { get; set; }

    /// <summary>Embedded outbox. Serializes to <c>"outbox"</c>, which is what the relay drains.</summary>
    public List<OutboxEvent> Outbox { get; set; } = new();

    /// <summary>Ids of commands already applied to this aggregate (dedupe / idempotency).</summary>
    public List<string> ProcessedCommands { get; set; } = new();

    public bool AlreadyProcessed(string commandId) => ProcessedCommands.Contains(commandId);

    public void MarkProcessed(string commandId)
    {
        if (!ProcessedCommands.Contains(commandId)) ProcessedCommands.Add(commandId);
    }

    /// <summary>Appends a domain event to the embedded outbox (committed in the same CAS as the state change).</summary>
    public void Emit(string type, object payload)
    {
        OutboxSeq++;
        Outbox.Add(new OutboxEvent
        {
            Type = type,
            Seq = OutboxSeq,
            Payload = JsonSerializer.Serialize(payload, ZarisDocumentStore.Json)
        });
    }
}
