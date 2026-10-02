using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;
using SagaOutbox.Saga;
using SagaOutbox.Services;

namespace SagaOutbox.Tests;

/// <summary>
/// A fully wired, isolated "world" for one test: an embedded Zaris store with a unique name (so each
/// test's keyspace is independent), the four services, the saga orchestrator, the event bus and the
/// relay — all sharing the one in-proc engine, exactly as the demo wires them. Every test therefore
/// exercises the REAL Zaris client + CAS path, not a mock.
/// </summary>
public sealed class World
{
    public const string Topic = "domain-events";

    public ZarisDocumentStore Store { get; private set; } = null!;
    public OrderService Orders { get; private set; } = null!;
    public PaymentService Payments { get; private set; } = null!;
    public InventoryService Inventory { get; private set; } = null!;
    public ShippingService Shipping { get; private set; } = null!;
    public EventBus Bus { get; private set; } = null!;
    public OutboxRelay Relay { get; private set; } = null!;

    public static async Task<World> NewAsync()
    {
        var client = await ZarisConnection.ConnectAsync($"zaris://inproc/test-{Guid.NewGuid():N}");
        var store = new ZarisDocumentStore(client);
        var w = new World
        {
            Store = store,
            Orders = new OrderService(store),
            Payments = new PaymentService(store),
            Inventory = new InventoryService(store),
            Shipping = new ShippingService(store),
            Bus = new EventBus(store),
        };
        w.Relay = new OutboxRelay(store, w.Bus, Topic);
        return w;
    }

    public OrderFulfillmentSaga NewSaga() =>
        new(Store, Orders, Payments, Inventory, Shipping);

    /// <summary>Pumps every service's outbox to the bus (one relay cycle across the whole system).</summary>
    public async Task<int> PumpAllAsync()
    {
        var n = 0;
        n += await Relay.PumpAsync(Orders.Registry, "order");
        n += await Relay.PumpAsync(Payments.Registry, "payment");
        n += await Relay.PumpAsync(Inventory.Registry, "inventory");
        n += await Relay.PumpAsync(Shipping.Registry, "shipping");
        return n;
    }
}
