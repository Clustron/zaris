using LiveBetting.Core.Domain;
using LiveBetting.Core.Services;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>
/// Proves TTL is applied at WRITE time (via <c>PutOptions.Metadata.Ttl</c>): a stale-odds rejection
/// leaves a short-lived requote token that carries a live TTL and self-reclaims.
/// </summary>
public class WriteTimeTtlTests
{
    [Fact]
    public async Task Stale_odds_rejection_writes_a_requote_token_with_a_live_ttl()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        await e.Markets.UpdateOddsAsync(ev, mkt, new Dictionary<string, decimal> { ["home"] = 2.75m });

        var r = await e.Betting.PlaceBetAsync(new PlaceBetRequest(
            "bet-stale", "alice", ev, mkt, "home", 10_00, QuotedOdds: 2.00m, OddsToleranceAbs: 0.10m));
        Assert.Equal(RejectReason.OddsChanged, r.Reason);

        var token = await e.Betting.GetRequoteAsync("bet-stale");
        Assert.NotNull(token);
        Assert.Equal(2.75m, token!.FreshOdds);

        var ttl = await e.Betting.GetRequoteTtlAsync("bet-stale");
        Assert.NotNull(ttl);                                  // TTL was set at write time
        Assert.True(ttl!.Value > TimeSpan.Zero);
        Assert.True(ttl.Value <= BettingService.RequoteTtl);
    }

    [Fact]
    public async Task No_requote_token_is_written_for_an_accepted_bet()
    {
        var clock = TestHarness.Clock();
        var (e, ev, mkt) = await TestHarness.MarketWithFundedUserAsync(clock);
        await using var _e = e;

        var r = await e.Betting.PlaceBetAsync(new PlaceBetRequest(
            "bet-ok", "alice", ev, mkt, "home", 10_00, QuotedOdds: 2.00m, OddsToleranceAbs: 0.10m));
        Assert.True(r.Accepted);

        Assert.Null(await e.Betting.GetRequoteAsync("bet-ok"));
    }
}
