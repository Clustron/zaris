using LiveBetting.Core.Domain;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>Proves market suspension blocks bets, and resume restores them at fresh odds.</summary>
public class SuspensionTests
{
    private static PlaceBetRequest Req(string betId, decimal quoted = 2.00m) =>
        new(betId, "alice", "match-1", "winner", "home", 10_00, quoted, OddsToleranceAbs: 1.00m);

    [Fact]
    public async Task Bets_are_rejected_while_the_market_is_suspended()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        await e.Markets.SuspendAsync(ev, mkt); // e.g. a goal was scored

        var r = await e.Betting.PlaceBetAsync(Req("bet-1"));

        Assert.False(r.Accepted);
        Assert.Equal(RejectReason.MarketSuspended, r.Reason);
        Assert.Equal(100_00, await e.Wallets.GetBalanceAsync("alice")); // no stake taken
    }

    [Fact]
    public async Task Bets_are_accepted_again_after_resume_at_new_odds()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        await e.Markets.SuspendAsync(ev, mkt);
        await e.Markets.ResumeAsync(ev, mkt, new Dictionary<string, decimal> { ["home"] = 1.50m });

        var r = await e.Betting.PlaceBetAsync(Req("bet-1", quoted: 1.50m));

        Assert.True(r.Accepted);
        Assert.Equal(1.50m, r.AcceptedOdds);
    }

    [Fact]
    public async Task Suspending_a_storm_of_in_flight_bets_takes_no_stake_after_the_suspension_point()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock, balanceMinor: 10_000_00);
        await using var _e = e;

        // Fire 200 placements; suspend concurrently partway through. Every accepted bet must have been
        // struck while Open; every bet seeing the suspension must be refused. Money stays exact either way.
        var placeTasks = Enumerable.Range(0, 200)
            .Select(i => e.Betting.PlaceBetAsync(Req($"bet-{i}")))
            .ToArray();
        await e.Markets.SuspendAsync(ev, mkt);
        var results = await Task.WhenAll(placeTasks);

        var accepted = results.Count(r => r.Accepted);
        var suspended = results.Count(r => r.Reason == RejectReason.MarketSuspended);

        Assert.Equal(200, accepted + suspended); // only these two outcomes are possible here
        // Balance exactly reflects the accepted bets — never more, never negative.
        Assert.Equal(10_000_00 - accepted * 10_00, await e.Wallets.GetBalanceAsync("alice"));
    }
}
