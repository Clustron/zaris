using LiveBetting.Core.Domain;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>Proves odds-staleness protection: a bet whose quoted odds drifted past tolerance is requoted, not struck.</summary>
public class StaleOddsTests
{
    [Fact]
    public async Task Bet_is_accepted_when_odds_are_within_tolerance()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        // live odds are 2.00; bettor quoted 1.95, tolerance 0.10 → within tolerance
        var r = await e.Betting.PlaceBetAsync(new PlaceBetRequest(
            "bet-1", "alice", ev, mkt, "home", 10_00, QuotedOdds: 1.95m, OddsToleranceAbs: 0.10m));

        Assert.True(r.Accepted);
        Assert.Equal(2.00m, r.AcceptedOdds); // struck at the live price, not the quote
    }

    [Fact]
    public async Task Bet_is_rejected_and_requoted_when_odds_moved_past_tolerance()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        // Odds drift from 2.00 to 2.50 between quote and placement.
        await e.Markets.UpdateOddsAsync(ev, mkt, new Dictionary<string, decimal> { ["home"] = 2.50m });

        var r = await e.Betting.PlaceBetAsync(new PlaceBetRequest(
            "bet-1", "alice", ev, mkt, "home", 10_00, QuotedOdds: 2.00m, OddsToleranceAbs: 0.10m));

        Assert.False(r.Accepted);
        Assert.Equal(BetStatus.Rejected, r.Status);
        Assert.Equal(RejectReason.OddsChanged, r.Reason);
        Assert.Equal(2.50m, r.RequoteOdds);
        // No money moved on a requote.
        Assert.Equal(100_00, await e.Wallets.GetBalanceAsync("alice"));
    }

    [Fact]
    public async Task Unknown_selection_is_rejected()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        var r = await e.Betting.PlaceBetAsync(new PlaceBetRequest(
            "bet-1", "alice", ev, mkt, "draw", 10_00, QuotedOdds: 2.00m));

        Assert.False(r.Accepted);
        Assert.Equal(RejectReason.UnknownSelection, r.Reason);
        Assert.Equal(100_00, await e.Wallets.GetBalanceAsync("alice"));
    }
}
