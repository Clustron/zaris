using LeaderboardPresence.Core;
using Xunit;

namespace LeaderboardPresence.Tests;

public class WindowedLeaderboardTests
{
    [Fact]
    public async Task Daily_window_resets_when_the_clock_rolls_to_the_next_day()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SubmitScoreAsync("arena", "alice", 100);
        Assert.Equal(1, await lb.GetCountAsync("arena", LeaderboardWindow.Daily));
        Assert.Equal(100, (await lb.GetRankAsync("arena", "alice", LeaderboardWindow.Daily))!.Score);

        // Roll to the next UTC day: the daily window key rotates, so the new day's board starts empty.
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await lb.GetCountAsync("arena", LeaderboardWindow.Daily));

        // ...while the all-time window still carries the score.
        Assert.Equal(100, (await lb.GetRankAsync("arena", "alice"))!.Score);
    }

    [Fact]
    public async Task Weekly_window_is_independent_of_daily_and_all_time()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SubmitScoreAsync("arena", "alice", 40);
        clock.Advance(TimeSpan.FromDays(1)); // next day, same ISO week
        await lb.SubmitScoreAsync("arena", "alice", 60);

        Assert.Equal(60, (await lb.GetRankAsync("arena", "alice", LeaderboardWindow.Daily))!.Score);   // only today's
        Assert.Equal(100, (await lb.GetRankAsync("arena", "alice", LeaderboardWindow.Weekly))!.Score); // both days
        Assert.Equal(100, (await lb.GetRankAsync("arena", "alice"))!.Score);                           // all-time
    }

    [Fact]
    public async Task Writing_to_rolling_windows_can_be_disabled()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, lb: new LeaderboardOptions { WriteToRollingWindows = false });
        var lb = sys.Leaderboard;

        await lb.SubmitScoreAsync("arena", "alice", 100);

        Assert.Equal(1, await lb.GetCountAsync("arena"));                         // all-time written
        Assert.Equal(0, await lb.GetCountAsync("arena", LeaderboardWindow.Daily)); // daily skipped
    }

    [Fact]
    public async Task ReapExpiredWindows_removes_day_keys_older_than_retention()
    {
        var clock = TestHarness.Clock();
        var opts = new LeaderboardOptions { DailyRetention = TimeSpan.FromDays(2) };
        await using var sys = await TestHarness.NewAsync(clock, lb: opts);
        var lb = sys.Leaderboard;

        // Day 0: write a daily entry.
        await lb.SubmitScoreAsync("arena", "alice", 10);
        // Advance 5 days (> 2-day retention). The day-0 daily key is now stale.
        clock.Advance(TimeSpan.FromDays(5));

        var removed = await lb.ReapExpiredWindowsAsync("arena");

        Assert.True(removed >= 1, $"expected at least one stale daily key reaped, got {removed}");
    }
}
