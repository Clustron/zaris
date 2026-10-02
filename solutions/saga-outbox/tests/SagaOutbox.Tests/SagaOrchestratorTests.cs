using SagaOutbox.Saga;
using SagaOutbox.Services;
using Xunit;

namespace SagaOutbox.Tests;

/// <summary>Saga forward flow, compensation, ordering and crash recovery.</summary>
public class SagaOrchestratorTests
{
    private static async Task Seed(World w, long balance = 100_000, int stock = 100)
    {
        await w.Payments.EnsureAccountAsync("cust", balance);
        await w.Inventory.EnsureStockAsync("widget", stock);
        await w.Orders.PlaceOrderAsync("ord-1", "cust", "widget", 2, 5_000);
    }

    [Fact]
    public async Task Happy_path_completes_and_leaves_consistent_state()
    {
        var w = await World.NewAsync();
        await Seed(w);

        var status = await w.NewSaga().StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000);

        Assert.Equal(SagaStatus.Completed, status);
        Assert.Equal(95_000, await w.Payments.GetBalanceAsync("cust"));
        var (available, reserved) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(98, available);
        Assert.Equal(2, reserved);
        Assert.Equal(ShipmentStatus.Created, await w.Shipping.GetStatusAsync("ord-1"));
        Assert.Equal(OrderStatus.Confirmed, (await w.Orders.GetAsync("ord-1"))!.Status);
    }

    [Fact]
    public async Task Out_of_stock_triggers_compensation_that_refunds_the_payment()
    {
        var w = await World.NewAsync();
        await Seed(w, balance: 100_000, stock: 1); // only 1 in stock, order wants 2

        var status = await w.NewSaga().StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000);

        Assert.Equal(SagaStatus.Compensated, status);
        Assert.Equal(100_000, await w.Payments.GetBalanceAsync("cust")); // charge refunded
        var (available, _) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(1, available);                                      // nothing reserved
        Assert.NotEqual(OrderStatus.Confirmed, (await w.Orders.GetAsync("ord-1"))!.Status);
        Assert.Equal(ShipmentStatus.None, await w.Shipping.GetStatusAsync("ord-1"));
    }

    [Fact]
    public async Task Payment_declined_on_first_step_needs_no_compensation()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("cust", 1_000); // too little
        await w.Inventory.EnsureStockAsync("widget", 100);
        await w.Orders.PlaceOrderAsync("ord-1", "cust", "widget", 2, 5_000);

        var status = await w.NewSaga().StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000);

        Assert.Equal(SagaStatus.Compensated, status);
        Assert.Equal(1_000, await w.Payments.GetBalanceAsync("cust")); // untouched
        var (available, reserved) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(100, available);
        Assert.Equal(0, reserved);
    }

    [Fact]
    public async Task Compensation_runs_completed_steps_in_strict_reverse_order()
    {
        var w = await World.NewAsync();
        await Seed(w);
        w.Shipping.DeclineNextCreate = true; // fail at step 3 after charge + reserve succeed

        var order = new List<string>();
        var saga = w.NewSaga();
        saga.OnCompensated = order.Add;

        var status = await saga.StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000);

        Assert.Equal(SagaStatus.Compensated, status);
        Assert.Equal(new[] { "ReserveInventory", "ChargePayment" }, order); // reverse of done order
        Assert.Equal(100_000, await w.Payments.GetBalanceAsync("cust"));
        var (available, reserved) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(100, available);
        Assert.Equal(0, reserved);
    }

    [Fact]
    public async Task Crash_mid_saga_resumes_and_completes_without_double_effects()
    {
        var w = await World.NewAsync();
        await Seed(w);

        // First orchestrator: crash right after ReserveInventory's effect commits, before the saga log
        // records the step as Done (the hardest crash window).
        var crashed = w.NewSaga();
        var crashArmed = true;
        crashed.AfterActionBeforePersist = (stepName, _) =>
        {
            if (stepName == "ReserveInventory" && crashArmed)
            {
                crashArmed = false;
                throw new Exception("orchestrator crashed");
            }
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<Exception>(() =>
            crashed.StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000));

        // The inventory service DID commit the reservation before the crash...
        var (availAfterCrash, _) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(98, availAfterCrash);
        // ...but the saga log had not recorded the step.
        var midDoc = await crashed.LoadAsync("saga-1");
        Assert.Equal(StepStatus.Pending, midDoc.StepFor("ReserveInventory").Status);

        // A fresh orchestrator resumes from the persisted log and finishes the flow.
        var resumed = w.NewSaga();
        var status = await resumed.ResumeAsync("saga-1");

        Assert.Equal(SagaStatus.Completed, status);
        Assert.Equal(95_000, await w.Payments.GetBalanceAsync("cust"));   // charged exactly once
        var (available, reserved) = await w.Inventory.GetLevelsAsync("widget");
        Assert.Equal(98, available);                                       // reserved exactly once
        Assert.Equal(2, reserved);
        Assert.Equal(ShipmentStatus.Created, await w.Shipping.GetStatusAsync("ord-1"));
        Assert.Equal(OrderStatus.Confirmed, (await w.Orders.GetAsync("ord-1"))!.Status);
    }

    [Fact]
    public async Task Resuming_a_completed_saga_is_a_no_op()
    {
        var w = await World.NewAsync();
        await Seed(w);
        var saga = w.NewSaga();
        Assert.Equal(SagaStatus.Completed, await saga.StartAsync("saga-1", "ord-1", "cust", "widget", 2, 5_000));

        // Resume again; balance must not move.
        Assert.Equal(SagaStatus.Completed, await w.NewSaga().ResumeAsync("saga-1"));
        Assert.Equal(95_000, await w.Payments.GetBalanceAsync("cust"));
    }
}
