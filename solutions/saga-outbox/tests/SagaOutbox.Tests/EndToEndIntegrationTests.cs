using SagaOutbox.Outbox;
using SagaOutbox.Saga;
using SagaOutbox.Services;
using Xunit;

namespace SagaOutbox.Tests;

/// <summary>
/// Full-stack integration across all four services, the saga orchestrator, the outbox relay and a
/// deduping consumer — everything sharing ONE embedded Zaris store. Proves the end-to-end reliability
/// claims: crash-recovery and effectively-once event effects.
/// </summary>
public class EndToEndIntegrationTests
{
    private static async Task Seed(World w, long balance, int stock)
    {
        await w.Payments.EnsureAccountAsync("cust", balance);
        await w.Inventory.EnsureStockAsync("widget", stock);
        await w.Orders.PlaceOrderAsync("ord-1", "cust", "widget", 2, 5_000);
    }

    [Fact]
    public async Task Happy_path_events_reach_a_consumer_exactly_once()
    {
        var w = await World.NewAsync();
        await Seed(w, 100_000, 100);

        await w.NewSaga().StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000);

        // Relay drains every service's outbox onto the bus (run a few cycles, as events accrue).
        for (var i = 0; i < 3; i++) await w.PumpAllAsync();

        // An "audit" consumer projects every event; count per event id to prove no duplicate effects.
        var consumer = new BusConsumer(w.Store, w.Bus, World.Topic, "audit");
        var byId = new Dictionary<string, int>();
        await consumer.PollAsync(rec =>
        {
            byId[rec.EventId] = byId.GetValueOrDefault(rec.EventId) + 1;
            return Task.CompletedTask;
        });

        // The happy path emits: OrderPlaced, PaymentCharged, InventoryReserved, ShipmentCreated, OrderConfirmed.
        Assert.True(byId.Count >= 5);
        Assert.All(byId.Values, c => Assert.Equal(1, c)); // every event applied exactly once
    }

    [Fact]
    public async Task Crash_and_recovery_yields_exactly_once_effects_across_all_services()
    {
        var w = await World.NewAsync();
        await Seed(w, 100_000, 100);

        // Crash after CreateShipment commits but before the saga records it.
        var armed = true;
        var crashed = w.NewSaga();
        crashed.AfterActionBeforePersist = (step, _) =>
        {
            if (step == "CreateShipment" && armed) { armed = false; throw new Exception("crash"); }
            return Task.CompletedTask;
        };
        await Assert.ThrowsAsync<Exception>(() => crashed.StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000));

        // Relay may already have shipped some outbox events; it can even redeliver duplicates.
        for (var i = 0; i < 2; i++) await w.PumpAllAsync();

        // Recover.
        Assert.Equal(SagaStatus.Completed, await w.NewSaga().ResumeAsync("saga-1"));
        for (var i = 0; i < 3; i++) await w.PumpAllAsync();

        // Business effects applied exactly once despite the crash + resume.
        Assert.Equal(95_000, await w.Payments.GetBalanceAsync("cust"));
        var (available, reserved) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(98, available);
        Assert.Equal(2, reserved);
        Assert.Equal(ShipmentStatus.Created, await w.Shipping.GetStatusAsync("ord-1"));
        Assert.Equal(OrderStatus.Confirmed, (await w.Orders.GetAsync("ord-1"))!.Status);

        // And a consumer sees each event id exactly once even though the bus may hold duplicates.
        var consumer = new BusConsumer(w.Store, w.Bus, World.Topic, "audit");
        var byId = new Dictionary<string, int>();
        await consumer.PollAsync(rec => { byId[rec.EventId] = byId.GetValueOrDefault(rec.EventId) + 1; return Task.CompletedTask; });
        Assert.All(byId.Values, c => Assert.Equal(1, c));
    }
}
