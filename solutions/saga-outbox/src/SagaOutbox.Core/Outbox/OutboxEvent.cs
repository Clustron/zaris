namespace SagaOutbox.Outbox;

/// <summary>
/// A domain event sitting in an aggregate's <b>embedded outbox</b>. The crucial property is that an
/// outbox event is written into the <i>same document</i> as the business-state change that produced
/// it, in the <i>same</i> compare-and-swap. There is therefore no window in which the state is
/// committed but the event is lost (or vice-versa): that is the dual-write problem, and co-locating
/// the two in one atomic document write is how this solution eliminates it.
///
/// A relay later moves dispatched=false events onto the shared event bus and flips the flag.
/// </summary>
public sealed record OutboxEvent
{
    /// <summary>Stable, unique id. Used end-to-end for idempotency / dedupe (relay and consumers).</summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Event type name, e.g. <c>PaymentCharged</c>.</summary>
    public string Type { get; init; } = "";

    /// <summary>Opaque JSON payload describing what happened.</summary>
    public string Payload { get; init; } = "{}";

    /// <summary>Monotonic per-aggregate sequence number (order of events within one aggregate).</summary>
    public long Seq { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>True once the relay has delivered the event to the bus.</summary>
    public bool Dispatched { get; init; }
}
