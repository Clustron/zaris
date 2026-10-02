using LiveBetting.Core;
using LiveBetting.Core.Domain;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>
/// End-to-end lifecycle on one shared store across two independent engine connections (proving the
/// in-proc store is shared by connection name): create → live odds → place → suspend → resume → settle,
/// with the money-conservation invariant asserted at the end.
/// </summary>
public class LifecycleIntegrationTests
{
    [Fact]
    public async Task Full_place_suspend_resume_settle_lifecycle_conserves_money_and_settles_once()
    {
        var clock = TestHarness.Clock();
        var store = TestHarness.NewStoreName();

        // Two separate connections to the same store: a "feed/ops" engine and a "bettor" engine.
        await using var ops = await BettingEngine.ConnectAsync($"zaris://inproc/{store}", clock);
        await using var bettor = await BettingEngine.ConnectAsync($"zaris://inproc/{store}", clock);

        const string ev = "match-7", mkt = "winner";
        await ops.Markets.CreateMarketAsync(ev, mkt, "Match winner",
            new Dictionary<string, decimal> { ["home"] = 2.00m, ["away"] = 3.00m });

        // Three funded bettors; opening float we will conserve against.
        long opening = 0;
        foreach (var u in new[] { "u1", "u2", "u3" })
            opening += await ops.Wallets.EnsureWalletAsync(u, 50_00);

        // Live odds tick (seen by the bettor connection's feed).
        await ops.Markets.UpdateOddsAsync(ev, mkt, new Dictionary<string, decimal> { ["home"] = 1.90m });

        // Bets placed via the OTHER connection.
        var b1 = await bettor.Betting.PlaceBetAsync(new PlaceBetRequest("b1", "u1", ev, mkt, "home", 20_00, 1.90m));
        var b2 = await bettor.Betting.PlaceBetAsync(new PlaceBetRequest("b2", "u2", ev, mkt, "away", 20_00, 3.00m));
        Assert.True(b1.Accepted);
        Assert.True(b2.Accepted);

        // A goal is scored → suspend; a bet placed now is refused. (A new attempt gets a new bet id.)
        await ops.Markets.SuspendAsync(ev, mkt);
        var refused = await bettor.Betting.PlaceBetAsync(new PlaceBetRequest("b3-early", "u3", ev, mkt, "home", 20_00, 1.90m, OddsToleranceAbs: 1m));
        Assert.Equal(RejectReason.MarketSuspended, refused.Reason);

        // Resume with new odds; now u3 can bet.
        await ops.Markets.ResumeAsync(ev, mkt, new Dictionary<string, decimal> { ["home"] = 1.60m, ["away"] = 4.00m });
        var b3 = await bettor.Betting.PlaceBetAsync(new PlaceBetRequest("b3", "u3", ev, mkt, "home", 20_00, 1.60m));
        Assert.True(b3.Accepted);
        Assert.Equal(1.60m, b3.AcceptedOdds);

        // Settle: home wins. u1 (home@1.90) and u3 (home@1.60) win; u2 (away) loses.
        var summary = await ops.Settlement.SettleMarketAsync(ev, mkt, "home");
        Assert.Equal(3, summary.Settled);
        Assert.Equal(2, summary.Won);
        Assert.Equal(1, summary.Lost);

        // Balances: u1 = 50 - 20 + 38 = 68 ; u2 = 50 - 20 = 30 ; u3 = 50 - 20 + 32 = 62
        Assert.Equal(68_00, await ops.Wallets.GetBalanceAsync("u1"));
        Assert.Equal(30_00, await ops.Wallets.GetBalanceAsync("u2"));
        Assert.Equal(62_00, await ops.Wallets.GetBalanceAsync("u3"));

        // Money conservation: opening float == sum(wallets) + house net (stakes collected - payouts paid).
        long finalWallets = 0;
        foreach (var u in new[] { "u1", "u2", "u3" })
            finalWallets += await ops.Wallets.GetBalanceAsync(u);
        long houseNet = summary.TotalStakeMinor - summary.TotalPayoutMinor; // 60 staked - 70 paid = -10
        Assert.Equal(opening, finalWallets + houseNet);

        // Re-settling changes nothing (exactly-once).
        var again = await ops.Settlement.SettleMarketAsync(ev, mkt, "home");
        Assert.Equal(0, again.Settled);
        Assert.Equal(68_00, await ops.Wallets.GetBalanceAsync("u1"));
    }
}
