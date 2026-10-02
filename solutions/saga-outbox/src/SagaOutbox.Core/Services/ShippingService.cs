using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;

namespace SagaOutbox.Services;

public enum ShipmentStatus { None, Created, Cancelled }

/// <summary>Shipment aggregate keyed by order id.</summary>
public sealed class Shipment : OutboxAggregate
{
    public string OrderId { get; set; } = "";
    public ShipmentStatus Status { get; set; } = ShipmentStatus.None;
    public string ShipmentId { get; set; } = "";
}

/// <summary>
/// Owns the shipping keyspace (<c>shipping:order:&lt;order&gt;</c>). Creating a shipment emits
/// <c>ShipmentCreated</c>; cancelling (the compensation) emits <c>ShipmentCancelled</c>. Idempotent
/// on command id. A <see cref="FailNextCreate"/> hook lets the demo inject a carrier failure.
/// </summary>
public sealed class ShippingService
{
    private readonly ZarisDocumentStore _store;
    private readonly OutboxRegistry _registry;

    public ShippingService(ZarisDocumentStore store)
    {
        _store = store;
        _registry = new OutboxRegistry(store, "shipping");
    }

    public OutboxRegistry Registry => _registry;
    public static string Key(string orderId) => $"shipping:order:{orderId}";

    /// <summary>If set, the next <see cref="CreateShipmentAsync"/> throws before writing (simulates a
    /// transient/crash-style failure that the saga sees as an exception).</summary>
    public bool FailNextCreate { get; set; }

    /// <summary>If set, the next <see cref="CreateShipmentAsync"/> returns an empty id without writing
    /// (simulates a <i>business</i> rejection the saga treats as a step failure → compensation).</summary>
    public bool DeclineNextCreate { get; set; }

    public async Task<ShipmentStatus> GetStatusAsync(string orderId, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<Shipment>(Key(orderId), ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.Status : ShipmentStatus.None;
    }

    public async Task<string> CreateShipmentAsync(string orderId, string commandId, CancellationToken ct = default)
    {
        if (FailNextCreate)
        {
            FailNextCreate = false;
            throw new InvalidOperationException("carrier unavailable (injected failure)");
        }
        if (DeclineNextCreate)
        {
            DeclineNextCreate = false;
            return ""; // business rejection: no shipment created, no state change
        }

        string shipmentId = "";
        await _store.MutateAsync<Shipment>(
            Key(orderId),
            s =>
            {
                if (s.AlreadyProcessed(commandId)) { shipmentId = s.ShipmentId; return null; }
                s.MarkProcessed(commandId);
                s.Status = ShipmentStatus.Created;
                s.ShipmentId = $"shp-{orderId}";
                shipmentId = s.ShipmentId;
                s.Emit("ShipmentCreated", new { commandId, orderId, shipmentId });
                return s;
            },
            create: () => new Shipment { OrderId = orderId },
            ct: ct).ConfigureAwait(false);
        await _registry.RegisterAsync(Key(orderId), ct).ConfigureAwait(false);
        return shipmentId;
    }

    public async Task CancelShipmentAsync(string orderId, string commandId, CancellationToken ct = default)
    {
        await _store.MutateAsync<Shipment>(
            Key(orderId),
            s =>
            {
                if (s.AlreadyProcessed(commandId)) return null;
                s.MarkProcessed(commandId);
                if (s.Status == ShipmentStatus.Created)
                {
                    s.Status = ShipmentStatus.Cancelled;
                    s.Emit("ShipmentCancelled", new { commandId, orderId, shipmentId = s.ShipmentId });
                    return s;
                }
                s.Emit("ShipmentCancelNoop", new { commandId, orderId });
                return s;
            },
            create: () => new Shipment { OrderId = orderId },
            ct: ct).ConfigureAwait(false);
    }
}
