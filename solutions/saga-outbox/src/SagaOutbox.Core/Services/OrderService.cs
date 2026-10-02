using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;

namespace SagaOutbox.Services;

public enum OrderStatus { Pending, Confirmed, Cancelled }

/// <summary>Order aggregate: the business request the saga fulfills.</summary>
public sealed class Order : OutboxAggregate
{
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public long AmountCents { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
}

/// <summary>
/// Owns the order keyspace (<c>order:&lt;order&gt;</c>). Placing an order records it Pending and emits
/// <c>OrderPlaced</c>; the saga later confirms (<c>OrderConfirmed</c>) or cancels (<c>OrderCancelled</c>).
/// </summary>
public sealed class OrderService
{
    private readonly ZarisDocumentStore _store;
    private readonly OutboxRegistry _registry;

    public OrderService(ZarisDocumentStore store)
    {
        _store = store;
        _registry = new OutboxRegistry(store, "order");
    }

    public OutboxRegistry Registry => _registry;
    public static string Key(string orderId) => $"order:{orderId}";

    public async Task PlaceOrderAsync(
        string orderId, string customerId, string sku, int qty, long amountCents, CancellationToken ct = default)
    {
        await _store.MutateAsync<Order>(
            Key(orderId),
            o =>
            {
                if (o.AlreadyProcessed("place")) return null;
                o.MarkProcessed("place");
                o.OrderId = orderId;
                o.CustomerId = customerId;
                o.Sku = sku;
                o.Quantity = qty;
                o.AmountCents = amountCents;
                o.Status = OrderStatus.Pending;
                o.Emit("OrderPlaced", new { orderId, customerId, sku, qty, amountCents });
                return o;
            },
            create: () => new Order { OrderId = orderId },
            ct: ct).ConfigureAwait(false);
        await _registry.RegisterAsync(Key(orderId), ct).ConfigureAwait(false);
    }

    public async Task<Order?> GetAsync(string orderId, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<Order>(Key(orderId), ct).ConfigureAwait(false);
        return doc.Found ? doc.Value : null;
    }

    public Task ConfirmAsync(string orderId, string commandId, CancellationToken ct = default)
        => Transition(orderId, commandId, OrderStatus.Confirmed, "OrderConfirmed", ct);

    public Task CancelAsync(string orderId, string commandId, CancellationToken ct = default)
        => Transition(orderId, commandId, OrderStatus.Cancelled, "OrderCancelled", ct);

    private Task Transition(string orderId, string commandId, OrderStatus status, string eventType, CancellationToken ct)
        => _store.MutateAsync<Order>(
            Key(orderId),
            o =>
            {
                if (o.AlreadyProcessed(commandId)) return null;
                o.MarkProcessed(commandId);
                o.Status = status;
                o.Emit(eventType, new { orderId, status = status.ToString() });
                return o;
            },
            create: () => new Order { OrderId = orderId },
            ct: ct);
}
