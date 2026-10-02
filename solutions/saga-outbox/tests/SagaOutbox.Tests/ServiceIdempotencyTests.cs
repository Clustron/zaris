using SagaOutbox.Services;
using Xunit;

namespace SagaOutbox.Tests;

/// <summary>Each service command is idempotent on its command id — the bedrock of safe saga retries.</summary>
public class ServiceIdempotencyTests
{
    [Fact]
    public async Task Charging_twice_with_the_same_command_id_debits_once()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 10_000);

        var r1 = await w.Payments.ChargeAsync("c1", "cmd-1", "o1", 3_000);
        var r2 = await w.Payments.ChargeAsync("c1", "cmd-1", "o1", 3_000); // retry

        Assert.True(r1.Charged);
        Assert.True(r2.Charged);
        Assert.Equal(7_000, await w.Payments.GetBalanceAsync("c1")); // debited only once
    }

    [Fact]
    public async Task Charge_is_declined_without_debiting_when_funds_are_insufficient()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 1_000);

        var r = await w.Payments.ChargeAsync("c1", "cmd-1", "o1", 3_000);

        Assert.False(r.Charged);
        Assert.Equal(1_000, await w.Payments.GetBalanceAsync("c1"));
    }

    [Fact]
    public async Task Refund_restores_balance_and_is_idempotent()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 10_000);
        await w.Payments.ChargeAsync("c1", "charge-1", "o1", 4_000);

        await w.Payments.RefundAsync("c1", "refund-1", "charge-1");
        await w.Payments.RefundAsync("c1", "refund-1", "charge-1"); // retry

        Assert.Equal(10_000, await w.Payments.GetBalanceAsync("c1")); // credited back exactly once
    }

    [Fact]
    public async Task Refund_of_a_charge_that_never_happened_is_a_safe_no_op()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 5_000);

        var balance = await w.Payments.RefundAsync("c1", "refund-1", "charge-that-never-ran");

        Assert.Equal(5_000, balance);
    }

    [Fact]
    public async Task Reserving_twice_with_the_same_command_id_reserves_once()
    {
        var w = await World.NewAsync();
        await w.Inventory.EnsureStockAsync("sku-1", 10);

        var r1 = await w.Inventory.ReserveAsync("sku-1", "cmd-1", "o1", 3);
        var r2 = await w.Inventory.ReserveAsync("sku-1", "cmd-1", "o1", 3); // retry

        Assert.True(r1.Reserved);
        Assert.True(r2.Reserved);
        var (available, reserved) = await w.Inventory.GetLevelsAsync("sku-1");
        Assert.Equal(7, available);
        Assert.Equal(3, reserved);
    }

    [Fact]
    public async Task Reserve_fails_when_out_of_stock_and_release_restores_levels()
    {
        var w = await World.NewAsync();
        await w.Inventory.EnsureStockAsync("sku-1", 2);

        var fail = await w.Inventory.ReserveAsync("sku-1", "cmd-1", "o1", 5);
        Assert.False(fail.Reserved);

        var ok = await w.Inventory.ReserveAsync("sku-1", "cmd-2", "o1", 2);
        Assert.True(ok.Reserved);

        await w.Inventory.ReleaseAsync("sku-1", "rel-1", "cmd-2");
        var (available, reserved) = await w.Inventory.GetLevelsAsync("sku-1");
        Assert.Equal(2, available);
        Assert.Equal(0, reserved);
    }
}
