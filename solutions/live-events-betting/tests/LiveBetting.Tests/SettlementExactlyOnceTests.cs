using LiveBetting.Core.Domain;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>Proves settlement pays winners, closes losers, and is exactly-once even when re-run (crash-safe).</summary>
public class SettlementExactlyOnceTests
{
    private const string Ev = "match-1", Mkt = "winner";

    private static async Task SeedAsync(LiveBetting.Core.BettingEngine e)
    {
        await e.Markets.CreateMarketAsync(Ev, Mkt, "Match winner",
            new Dictionary<string, decimal> { ["home"] = 2.00m, ["away"] = 4.00m });
        await e.Wallets.EnsureWalletAsync("winnerUser", 100_00);
        await e.Wallets.EnsureWalletAsync("loserUser", 100_00);
    }

    [Fact]
    public async Task Winners_are_paid_and_losers_are_closed()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await SeedAsync(e);

        await e.Betting.PlaceBetAsync(new PlaceBetRequest("w", "winnerUser", Ev, Mkt, "home", 10_00, 2.00m));
        await e.Betting.PlaceBetAsync(new PlaceBetRequest("l", "loserUser", Ev, Mkt, "away", 10_00, 4.00m));

        var summary = await e.Settlement.SettleMarketAsync(Ev, Mkt, winningSelection: "home");

        Assert.Equal(2, summary.Settled);
        Assert.Equal(1, summary.Won);
        Assert.Equal(1, summary.Lost);
        Assert.Equal(20_00, summary.TotalPayoutMinor); // 10.00 stake @ 2.00

        // Winner: 100 - 10 stake + 20 payout = 110.00
        Assert.Equal(110_00, await e.Wallets.GetBalanceAsync("winnerUser"));
        // Loser: 100 - 10 stake = 90.00
        Assert.Equal(90_00, await e.Wallets.GetBalanceAsync("loserUser"));

        Assert.Equal(BetStatus.Won, (await e.Betting.GetBetAsync("w"))!.Status);
        Assert.Equal(BetStatus.Lost, (await e.Betting.GetBetAsync("l"))!.Status);
    }

    [Fact]
    public async Task Re_running_settlement_does_not_double_pay()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await SeedAsync(e);

        await e.Betting.PlaceBetAsync(new PlaceBetRequest("w", "winnerUser", Ev, Mkt, "home", 10_00, 2.00m));

        var first = await e.Settlement.SettleMarketAsync(Ev, Mkt, "home");
        var balanceAfterFirst = await e.Wallets.GetBalanceAsync("winnerUser");

        // Re-run settlement three more times — simulating retried / crash-recovered settlement passes.
        var second = await e.Settlement.SettleMarketAsync(Ev, Mkt, "home");
        await e.Settlement.SettleMarketAsync(Ev, Mkt, "home");
        await e.Settlement.SettleMarketAsync(Ev, Mkt, "home");

        Assert.Equal(1, first.Won);
        Assert.Equal(0, second.Settled);                  // nothing left to settle
        Assert.Equal(110_00, balanceAfterFirst);
        Assert.Equal(110_00, await e.Wallets.GetBalanceAsync("winnerUser")); // unchanged — paid once
    }

    [Fact]
    public async Task Concurrent_settlement_passes_pay_exactly_once()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await SeedAsync(e);
        await e.Wallets.EnsureWalletAsync("winnerUser", 100_00);

        await e.Betting.PlaceBetAsync(new PlaceBetRequest("w", "winnerUser", Ev, Mkt, "home", 10_00, 2.00m));

        // Five settlement passes racing at once.
        await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => e.Settlement.SettleMarketAsync(Ev, Mkt, "home")));

        Assert.Equal(110_00, await e.Wallets.GetBalanceAsync("winnerUser")); // paid exactly once
    }

    [Fact]
    public async Task Settlement_is_written_to_a_durable_replayable_log()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await SeedAsync(e);

        await e.Betting.PlaceBetAsync(new PlaceBetRequest("w", "winnerUser", Ev, Mkt, "home", 10_00, 2.00m));
        await e.Betting.PlaceBetAsync(new PlaceBetRequest("l", "loserUser", Ev, Mkt, "away", 10_00, 4.00m));
        await e.Settlement.SettleMarketAsync(Ev, Mkt, "home");

        var log = await e.Settlement.ReadSettlementLogAsync(Ev, Mkt, 10);
        Assert.Equal(2, log.Count);
        Assert.Contains(log, r => r.BetId == "w" && r.Won && r.PayoutMinor == 20_00);
        Assert.Contains(log, r => r.BetId == "l" && !r.Won && r.PayoutMinor == 0);
    }
}
