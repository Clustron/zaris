using LeaderboardPresence.Core;
using Xunit;

namespace LeaderboardPresence.Tests;

public class PubSubStreamTests
{
    private static async Task<T> WaitAsync<T>(TaskCompletionSource<T> tcs, int ms = 3000)
    {
        var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
        Assert.True(done == tcs.Task, "timed out waiting for a live event");
        return tcs.Task.Result;
    }

    [Fact]
    public async Task Leaderboard_change_is_delivered_to_a_live_subscriber()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        var got = new TaskCompletionSource<LeaderboardChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await lb.SubscribeChangesAsync(c => { got.TrySetResult(c); return Task.CompletedTask; }, board: "arena");

        await lb.SubmitScoreAsync("arena", "alice", 500);

        var change = await WaitAsync(got);
        Assert.Equal("alice", change.Player);
        Assert.Equal(500, change.NewScore);
        Assert.Equal(1, change.NewRank);
        Assert.Equal("arena", change.Board);
    }

    [Fact]
    public async Task Pattern_subscription_receives_changes_from_any_board()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        var got = new TaskCompletionSource<LeaderboardChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await lb.SubscribeChangesAsync(c => { got.TrySetResult(c); return Task.CompletedTask; }, board: null);

        await lb.SubmitScoreAsync("some-other-arena", "zoe", 42);

        var change = await WaitAsync(got);
        Assert.Equal("zoe", change.Player);
        Assert.Equal("some-other-arena", change.Board);
    }

    [Fact]
    public async Task Presence_transition_is_delivered_to_a_live_subscriber()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var p = sys.Presence;

        var got = new TaskCompletionSource<PresenceChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await p.SubscribeAsync(c => { got.TrySetResult(c); return Task.CompletedTask; });

        await p.HeartbeatAsync("alice", "mobile");

        var change = await WaitAsync(got);
        Assert.Equal("alice", change.Player);
        Assert.Equal(PresenceStatus.Online, change.Status);
        Assert.Equal(PresenceStatus.Offline, change.PreviousStatus);
    }

    [Fact]
    public async Task Leaderboard_changes_are_persisted_to_the_durable_audit_stream()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SubmitScoreAsync("arena", "alice", 10);
        await lb.SubmitScoreAsync("arena", "bob", 20);
        await lb.SubmitScoreAsync("arena", "alice", 5);

        var audit = await lb.ReadAuditAsync("arena", 10); // newest first
        Assert.Equal(3, audit.Count);
        Assert.Equal("alice", audit[0].Player);
        Assert.Equal(15, audit[0].NewScore);
    }

    [Fact]
    public async Task Presence_changes_are_persisted_to_the_durable_audit_stream()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var p = sys.Presence;

        await p.HeartbeatAsync("alice");
        await p.SetStatusAsync("alice", PresenceStatus.Offline);

        var audit = await p.ReadAuditAsync(10);
        Assert.Equal(2, audit.Count);
        Assert.Equal(PresenceStatus.Offline, audit[0].Status); // newest first
        Assert.Equal(PresenceStatus.Online, audit[1].Status);
    }
}
