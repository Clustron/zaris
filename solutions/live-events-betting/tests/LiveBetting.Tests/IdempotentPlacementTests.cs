using LiveBetting.Core.Domain;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>Proves placement is idempotent by client-supplied bet id: retries never double-place or double-debit.</summary>
public class IdempotentPlacementTests
{
    private static PlaceBetRequest Req(string betId, long stake = 10_00, decimal quoted = 2.00m) =>
        new(betId, "alice", "match-1", "winner", "home", stake, quoted);

    [Fact]
    public async Task A_successful_placement_debits_once_and_records_the_bet()
    {
        var clock = TestHarness.Clock();
        var (e, _, _) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        var r = await e.Betting.PlaceBetAsync(Req("bet-1", stake: 10_00));

        Assert.True(r.Accepted);
        Assert.Equal(BetStatus.Placed, r.Status);
        Assert.Equal(2.00m, r.AcceptedOdds);
        Assert.Equal(100_00 - 10_00, await e.Wallets.GetBalanceAsync("alice"));
    }

    [Fact]
    public async Task Replaying_the_same_bet_id_returns_the_same_outcome_without_a_second_debit()
    {
        var clock = TestHarness.Clock();
        var (e, _, _) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        var first = await e.Betting.PlaceBetAsync(Req("bet-1", stake: 10_00));
        var replay = await e.Betting.PlaceBetAsync(Req("bet-1", stake: 10_00));

        Assert.True(first.Accepted);
        Assert.True(replay.Accepted);
        Assert.False(first.WasReplay);
        Assert.True(replay.WasReplay);
        // Balance debited exactly once despite two placement calls with the same id.
        Assert.Equal(100_00 - 10_00, await e.Wallets.GetBalanceAsync("alice"));
    }

    [Fact]
    public async Task Concurrent_duplicate_submissions_place_exactly_one_bet()
    {
        var clock = TestHarness.Clock();
        var (e, _, _) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        // Same bet id submitted 25 times at once (a client hammering a flaky connection).
        var results = await Task.WhenAll(Enumerable.Range(0, 25)
            .Select(_ => e.Betting.PlaceBetAsync(Req("bet-dup", stake: 20_00))));

        Assert.All(results, r => Assert.True(r.Accepted));
        // Only one stake left the wallet.
        Assert.Equal(100_00 - 20_00, await e.Wallets.GetBalanceAsync("alice"));
    }

    [Fact]
    public async Task Distinct_bet_ids_place_distinct_bets()
    {
        var clock = TestHarness.Clock();
        var (e, _, _) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        await e.Betting.PlaceBetAsync(Req("bet-a", stake: 10_00));
        await e.Betting.PlaceBetAsync(Req("bet-b", stake: 15_00));

        Assert.Equal(100_00 - 25_00, await e.Wallets.GetBalanceAsync("alice"));
    }
}
