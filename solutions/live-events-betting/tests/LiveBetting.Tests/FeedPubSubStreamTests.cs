using LiveBetting.Core.Domain;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>Proves the live odds feed fans out over Zaris pub/sub and is durably replayable from a Zaris stream.</summary>
public class FeedPubSubStreamTests
{
    private const string Ev = "match-1", Mkt = "winner";

    private static async Task<T> WaitAsync<T>(TaskCompletionSource<T> tcs, int ms = 3000)
    {
        var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
        Assert.True(done == tcs.Task, "timed out waiting for a live feed event");
        return await tcs.Task;
    }

    [Fact]
    public async Task Odds_change_is_delivered_to_a_live_subscriber()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await e.Markets.CreateMarketAsync(Ev, Mkt, "Match winner",
            new Dictionary<string, decimal> { ["home"] = 2.00m });

        var got = new TaskCompletionSource<MarketEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await e.Markets.SubscribeAsync(ev => { got.TrySetResult(ev); return Task.CompletedTask; }, Ev, Mkt);

        await e.Markets.UpdateOddsAsync(Ev, Mkt, new Dictionary<string, decimal> { ["home"] = 1.80m });

        var change = await WaitAsync(got);
        Assert.Equal(MarketEventKind.OddsChanged, change.Kind);
        Assert.Equal(1.80m, change.Odds["home"]);
    }

    [Fact]
    public async Task Pattern_subscription_receives_suspend_and_resume_from_any_market()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await e.Markets.CreateMarketAsync(Ev, Mkt, "Match winner",
            new Dictionary<string, decimal> { ["home"] = 2.00m });

        var kinds = new List<MarketEventKind>();
        var twoSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await e.Markets.SubscribeAsync(ev =>
        {
            lock (kinds) { kinds.Add(ev.Kind); if (kinds.Count >= 2) twoSeen.TrySetResult(true); }
            return Task.CompletedTask;
        }, eventId: null, marketId: null); // pattern: every market

        await e.Markets.SuspendAsync(Ev, Mkt);
        await e.Markets.ResumeAsync(Ev, Mkt, new Dictionary<string, decimal> { ["home"] = 1.90m });

        await WaitAsync(twoSeen);
        Assert.Contains(MarketEventKind.Suspended, kinds);
        Assert.Contains(MarketEventKind.Resumed, kinds);
    }

    [Fact]
    public async Task Feed_history_is_persisted_to_the_durable_stream_in_order()
    {
        var clock = TestHarness.Clock();
        await using var e = await TestHarness.NewAsync(clock);
        await e.Markets.CreateMarketAsync(Ev, Mkt, "Match winner",
            new Dictionary<string, decimal> { ["home"] = 2.00m });

        await e.Markets.UpdateOddsAsync(Ev, Mkt, new Dictionary<string, decimal> { ["home"] = 1.90m });
        await e.Markets.SuspendAsync(Ev, Mkt);
        await e.Markets.ResumeAsync(Ev, Mkt, new Dictionary<string, decimal> { ["home"] = 2.20m });

        var history = await e.Markets.ReadFeedHistoryAsync(Ev, Mkt, 10); // newest first
        Assert.Equal(3, history.Count);
        Assert.Equal(MarketEventKind.Resumed, history[0].Kind);
        Assert.Equal(MarketEventKind.Suspended, history[1].Kind);
        Assert.Equal(MarketEventKind.OddsChanged, history[2].Kind);
    }
}
