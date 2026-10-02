using SagaOutbox.Infrastructure;
using SagaOutbox.Services;

namespace SagaOutbox.Saga;

/// <summary>Outcome of a forward step: success, or a <i>business</i> failure (not an exception).</summary>
public readonly record struct StepOutcome(bool Success, string Detail)
{
    public static StepOutcome Ok(string detail = "") => new(true, detail);
    public static StepOutcome Fail(string detail) => new(false, detail);
}

/// <summary>One step of the pipeline: a forward action and its compensating action.</summary>
internal sealed record SagaStep(
    string Name,
    Func<SagaDocument, CancellationToken, Task<StepOutcome>> Forward,
    Func<SagaDocument, CancellationToken, Task> Compensate);

/// <summary>
/// An <b>orchestration saga</b> for order fulfillment: Charge Payment → Reserve Inventory →
/// Create Shipment → Confirm Order. It drives the services forward one step at a time, and on any
/// failure runs the <b>compensations of the completed steps in reverse order</b> (refund, restock,
/// cancel shipment), leaving the system in a consistent "fully done" or "fully undone" state.
///
/// Reliability properties:
/// <list type="bullet">
///   <item><b>Durable &amp; resumable.</b> The saga log (<see cref="SagaDocument"/>) is persisted via
///         CAS after every transition. <see cref="ResumeAsync"/> rehydrates it and continues, so a
///         crash mid-saga never strands the flow.</item>
///   <item><b>Idempotent steps.</b> Every service command id is derived deterministically from the
///         saga id, so re-running a step after a crash is a no-op at the service (its idempotency
///         ledger has already seen that command) — no double charge, no double reservation.</item>
///   <item><b>Deterministic compensation.</b> Only steps recorded <c>Done</c> are compensated, in
///         strict reverse order.</item>
/// </list>
/// </summary>
public sealed class OrderFulfillmentSaga
{
    private readonly ZarisDocumentStore _store;
    private readonly List<SagaStep> _pipeline;

    /// <summary>Test/demo hook: invoked after a step's action succeeds but <b>before</b> the saga log
    /// records it. Throwing here simulates the orchestrator crashing in that window.</summary>
    public Func<string, SagaDocument, Task>? AfterActionBeforePersist { get; set; }

    /// <summary>Observability hooks (used by tests to assert ordering, and by the demo to narrate).</summary>
    public Action<string>? OnStepDone { get; set; }
    public Action<string>? OnCompensated { get; set; }

    public OrderFulfillmentSaga(
        ZarisDocumentStore store,
        OrderService orders,
        PaymentService payments,
        InventoryService inventory,
        ShippingService shipping)
    {
        _store = store;
        _pipeline = new List<SagaStep>
        {
            new("ChargePayment",
                async (s, ct) =>
                {
                    var r = await payments.ChargeAsync(s.CustomerId, Cmd(s, "charge"), s.OrderId, s.AmountCents, ct);
                    return r.Charged ? StepOutcome.Ok($"charged {s.AmountCents}c, balance {r.BalanceAfter}c")
                                     : StepOutcome.Fail("payment declined (insufficient funds)");
                },
                (s, ct) => payments.RefundAsync(s.CustomerId, Cmd(s, "refund"), Cmd(s, "charge"), ct)),

            new("ReserveInventory",
                async (s, ct) =>
                {
                    var r = await inventory.ReserveAsync(s.Sku, Cmd(s, "reserve"), s.OrderId, s.Quantity, ct);
                    return r.Reserved ? StepOutcome.Ok($"reserved {s.Quantity} of {s.Sku}")
                                      : StepOutcome.Fail($"out of stock for {s.Sku}");
                },
                (s, ct) => inventory.ReleaseAsync(s.Sku, Cmd(s, "restock"), Cmd(s, "reserve"), ct)),

            new("CreateShipment",
                async (s, ct) =>
                {
                    var id = await shipping.CreateShipmentAsync(s.OrderId, Cmd(s, "ship"), ct);
                    return string.IsNullOrEmpty(id) ? StepOutcome.Fail("carrier rejected shipment")
                                                    : StepOutcome.Ok($"shipment {id} created");
                },
                (s, ct) => shipping.CancelShipmentAsync(s.OrderId, Cmd(s, "cancel-ship"), ct)),

            new("ConfirmOrder",
                async (s, ct) =>
                {
                    await orders.ConfirmAsync(s.OrderId, Cmd(s, "confirm"), ct);
                    return StepOutcome.Ok("order confirmed");
                },
                // Compensation for the terminal step: cancel the order (only reached if a later step existed).
                (s, ct) => orders.CancelAsync(s.OrderId, Cmd(s, "cancel-order"), ct)),
        };
    }

    private static string Key(string sagaId) => $"saga:{sagaId}";
    private static string Cmd(SagaDocument s, string verb) => $"{s.SagaId}:{verb}";

    /// <summary>Creates the saga log (idempotent) and runs it to a terminal state.</summary>
    public async Task<SagaStatus> StartAsync(
        string sagaId, string orderId, string customerId, string sku, int qty, long amountCents, CancellationToken ct = default)
    {
        await _store.MutateAsync<SagaDocument>(
            Key(sagaId),
            doc => doc.Steps.Count > 0 ? null : Init(doc, sagaId, orderId, customerId, sku, qty, amountCents),
            create: () => new SagaDocument(),
            ct: ct).ConfigureAwait(false);
        return await RunAsync(sagaId, ct).ConfigureAwait(false);
    }

    /// <summary>Resumes an existing saga from its persisted log (same code path as a fresh run).</summary>
    public Task<SagaStatus> ResumeAsync(string sagaId, CancellationToken ct = default) => RunAsync(sagaId, ct);

    private SagaDocument Init(SagaDocument doc, string sagaId, string orderId, string customerId, string sku, int qty, long amountCents)
    {
        doc.SagaId = sagaId;
        doc.OrderId = orderId;
        doc.CustomerId = customerId;
        doc.Sku = sku;
        doc.Quantity = qty;
        doc.AmountCents = amountCents;
        doc.Status = SagaStatus.Running;
        doc.CurrentStep = 0;
        foreach (var step in _pipeline)
            doc.StepFor(step.Name);
        return doc;
    }

    private async Task<SagaStatus> RunAsync(string sagaId, CancellationToken ct)
    {
        // --- forward phase ---
        while (true)
        {
            var doc = await LoadAsync(sagaId, ct).ConfigureAwait(false);
            if (doc.Status != SagaStatus.Running)
                return await MaybeCompensateAsync(sagaId, doc, ct).ConfigureAwait(false);

            if (doc.CurrentStep >= _pipeline.Count)
            {
                await PersistAsync(sagaId, d => { d.Status = SagaStatus.Completed; return d; }, ct).ConfigureAwait(false);
                return SagaStatus.Completed;
            }

            var step = _pipeline[doc.CurrentStep];
            var outcome = await step.Forward(doc, ct).ConfigureAwait(false);

            // Crash window: the service effect is now committed, but the saga log has NOT yet recorded it.
            if (AfterActionBeforePersist is not null)
                await AfterActionBeforePersist(step.Name, doc).ConfigureAwait(false);

            if (outcome.Success)
            {
                await PersistAsync(sagaId, d =>
                {
                    d.StepFor(step.Name).Status = StepStatus.Done;
                    d.StepFor(step.Name).Detail = outcome.Detail;
                    d.CurrentStep++;
                    return d;
                }, ct).ConfigureAwait(false);
                OnStepDone?.Invoke(step.Name);
            }
            else
            {
                await PersistAsync(sagaId, d =>
                {
                    d.StepFor(step.Name).Status = StepStatus.Failed;
                    d.StepFor(step.Name).Detail = outcome.Detail;
                    d.Status = SagaStatus.Compensating;
                    d.FailureReason = outcome.Detail;
                    return d;
                }, ct).ConfigureAwait(false);
                var latest = await LoadAsync(sagaId, ct).ConfigureAwait(false);
                return await MaybeCompensateAsync(sagaId, latest, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<SagaStatus> MaybeCompensateAsync(string sagaId, SagaDocument doc, CancellationToken ct)
    {
        if (doc.Status == SagaStatus.Completed) return SagaStatus.Completed;
        if (doc.Status != SagaStatus.Compensating && doc.Status != SagaStatus.Compensated)
            return doc.Status;

        // Compensate every Done step in strict reverse pipeline order; idempotent + resumable.
        for (var i = _pipeline.Count - 1; i >= 0; i--)
        {
            var step = _pipeline[i];
            var rec = doc.StepFor(step.Name);
            if (rec.Status != StepStatus.Done) continue;

            await step.Compensate(doc, ct).ConfigureAwait(false);
            doc = await PersistAsync(sagaId, d =>
            {
                d.StepFor(step.Name).Status = StepStatus.Compensated;
                return d;
            }, ct).ConfigureAwait(false);
            OnCompensated?.Invoke(step.Name);
        }

        await PersistAsync(sagaId, d => { d.Status = SagaStatus.Compensated; return d; }, ct).ConfigureAwait(false);
        return SagaStatus.Compensated;
    }

    public async Task<SagaDocument> LoadAsync(string sagaId, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<SagaDocument>(Key(sagaId), ct).ConfigureAwait(false);
        if (!doc.Found || doc.Value is null)
            throw new InvalidOperationException($"saga '{sagaId}' not found");
        return doc.Value;
    }

    private Task<SagaDocument> PersistAsync(string sagaId, Func<SagaDocument, SagaDocument> mutate, CancellationToken ct)
        => _store.MutateAsync<SagaDocument>(Key(sagaId), d => mutate(d), create: () => new SagaDocument(), ct: ct);
}
