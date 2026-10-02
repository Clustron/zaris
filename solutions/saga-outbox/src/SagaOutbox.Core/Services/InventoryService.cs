using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;

namespace SagaOutbox.Services;

public readonly record struct ReserveResult(bool Reserved, int Quantity, int AvailableAfter);

/// <summary>A reservation recorded in stock, so a later release (restock) can find its quantity.</summary>
public sealed class ReservationRecord
{
    public string CommandId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public int Quantity { get; set; }
    public bool Released { get; set; }
}

/// <summary>Stock aggregate for one SKU: available/reserved counts + reservation ledger + outbox.</summary>
public sealed class Stock : OutboxAggregate
{
    public string Sku { get; set; } = "";
    public int Available { get; set; }
    public int Reserved { get; set; }
    public List<ReservationRecord> Reservations { get; set; } = new();
}

/// <summary>
/// Owns the stock keyspace (<c>inventory:stock:&lt;sku&gt;</c>). Reserving moves units from available
/// to reserved and emits <c>InventoryReserved</c> (or <c>InventoryReservationFailed</c> when short),
/// atomically. Releasing (the compensation) puts them back and emits <c>InventoryRestocked</c>.
/// Idempotent on command id.
/// </summary>
public sealed class InventoryService
{
    private readonly ZarisDocumentStore _store;
    private readonly OutboxRegistry _registry;

    public InventoryService(ZarisDocumentStore store)
    {
        _store = store;
        _registry = new OutboxRegistry(store, "inventory");
    }

    public OutboxRegistry Registry => _registry;
    public static string Key(string sku) => $"inventory:stock:{sku}";

    public async Task EnsureStockAsync(string sku, int available, CancellationToken ct = default)
    {
        await _store.CreateAsync(Key(sku), new Stock { Sku = sku, Available = available }, ct).ConfigureAwait(false);
        await _registry.RegisterAsync(Key(sku), ct).ConfigureAwait(false);
    }

    public async Task<(int Available, int Reserved)> GetLevelsAsync(string sku, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<Stock>(Key(sku), ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? (doc.Value.Available, doc.Value.Reserved) : (0, 0);
    }

    public async Task<ReserveResult> ReserveAsync(
        string sku, string commandId, string orderId, int qty, CancellationToken ct = default)
    {
        ReserveResult result = default;
        await _store.MutateAsync<Stock>(
            Key(sku),
            stock =>
            {
                if (stock.AlreadyProcessed(commandId))
                {
                    var prior = stock.Reservations.FirstOrDefault(r => r.CommandId == commandId);
                    result = prior is not null
                        ? new ReserveResult(true, prior.Quantity, stock.Available)
                        : new ReserveResult(false, qty, stock.Available);
                    return null;
                }

                stock.MarkProcessed(commandId);
                if (stock.Available < qty)
                {
                    stock.Emit("InventoryReservationFailed", new { commandId, orderId, sku, qty, available = stock.Available });
                    result = new ReserveResult(false, qty, stock.Available);
                    return stock;
                }

                stock.Available -= qty;
                stock.Reserved += qty;
                stock.Reservations.Add(new ReservationRecord { CommandId = commandId, OrderId = orderId, Quantity = qty });
                stock.Emit("InventoryReserved", new { commandId, orderId, sku, qty, availableAfter = stock.Available });
                result = new ReserveResult(true, qty, stock.Available);
                return stock;
            },
            create: () => new Stock { Sku = sku, Available = 0 },
            ct: ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>Compensation: releases a prior reservation back to available stock. Idempotent.</summary>
    public async Task<int> ReleaseAsync(
        string sku, string commandId, string originalReserveCommandId, CancellationToken ct = default)
    {
        int availableAfter = 0;
        await _store.MutateAsync<Stock>(
            Key(sku),
            stock =>
            {
                availableAfter = stock.Available;
                if (stock.AlreadyProcessed(commandId)) return null;

                stock.MarkProcessed(commandId);
                var res = stock.Reservations.FirstOrDefault(r => r.CommandId == originalReserveCommandId && !r.Released);
                if (res is null)
                {
                    stock.Emit("InventoryRestockNoop", new { commandId, originalReserveCommandId });
                    return stock;
                }

                res.Released = true;
                stock.Reserved -= res.Quantity;
                stock.Available += res.Quantity;
                availableAfter = stock.Available;
                stock.Emit("InventoryRestocked", new { commandId, originalReserveCommandId, sku, qty = res.Quantity, availableAfter });
                return stock;
            },
            create: () => new Stock { Sku = sku, Available = 0 },
            ct: ct).ConfigureAwait(false);
        return availableAfter;
    }
}
