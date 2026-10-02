using LeaderboardPresence.Core;
using Xunit;

namespace LeaderboardPresence.Tests;

public class PresenceTests
{
    private static PresenceOptions Opts() => new()
    {
        OnlineWindow = TimeSpan.FromSeconds(15),
        AwayWindow = TimeSpan.FromSeconds(60)
    };

    [Fact]
    public async Task Heartbeat_marks_player_online()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        var snap = await p.HeartbeatAsync("alice", "mobile");
        Assert.Equal(PresenceStatus.Online, snap.Status);
        Assert.Equal("mobile", snap.Detail);

        Assert.Equal(PresenceStatus.Online, (await p.GetAsync("alice")).Status);
    }

    [Fact]
    public async Task Status_decays_online_then_away_then_offline_as_time_passes()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        await p.HeartbeatAsync("alice");

        clock.Advance(TimeSpan.FromSeconds(10));                  // within online window
        Assert.Equal(PresenceStatus.Online, (await p.GetAsync("alice")).Status);

        clock.Advance(TimeSpan.FromSeconds(20));                  // 30s total -> away
        Assert.Equal(PresenceStatus.Away, (await p.GetAsync("alice")).Status);

        clock.Advance(TimeSpan.FromSeconds(40));                  // 70s total -> offline
        Assert.Equal(PresenceStatus.Offline, (await p.GetAsync("alice")).Status);
    }

    [Fact]
    public async Task Heartbeat_refreshes_player_back_to_online()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        await p.HeartbeatAsync("alice");
        clock.Advance(TimeSpan.FromSeconds(30)); // away
        Assert.Equal(PresenceStatus.Away, (await p.GetAsync("alice")).Status);

        await p.HeartbeatAsync("alice");         // fresh heartbeat
        Assert.Equal(PresenceStatus.Online, (await p.GetAsync("alice")).Status);
    }

    [Fact]
    public async Task Unknown_player_is_offline()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        Assert.Equal(PresenceStatus.Offline, (await sys.Presence.GetAsync("ghost")).Status);
    }

    [Fact]
    public async Task Manual_away_overrides_recent_heartbeat_until_next_heartbeat()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        await p.HeartbeatAsync("alice");
        await p.SetStatusAsync("alice", PresenceStatus.Away);
        Assert.Equal(PresenceStatus.Away, (await p.GetAsync("alice")).Status); // away despite being "active"

        await p.HeartbeatAsync("alice");
        Assert.Equal(PresenceStatus.Online, (await p.GetAsync("alice")).Status); // heartbeat clears override
    }

    [Fact]
    public async Task Logout_sets_offline_and_removes_from_index()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        await p.HeartbeatAsync("alice");
        await p.HeartbeatAsync("bob");
        await p.SetStatusAsync("alice", PresenceStatus.Offline);

        Assert.Equal(PresenceStatus.Offline, (await p.GetAsync("alice")).Status);
        var online = await p.ListOnlineAsync();
        Assert.Equal(new[] { "bob" }, online.Select(s => s.Player));
    }

    [Fact]
    public async Task ListByStatus_and_OnlineCount_classify_the_population()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        await p.HeartbeatAsync("fresh1");
        await p.HeartbeatAsync("fresh2");
        clock.Advance(TimeSpan.FromSeconds(30)); // these two become "away"
        await p.HeartbeatAsync("recent");        // online
        // fresh1/fresh2 last seen 30s ago -> away; recent -> online

        Assert.Equal(1, await p.OnlineCountAsync());
        var online = await p.ListOnlineAsync();
        Assert.Equal(new[] { "recent" }, online.Select(s => s.Player));

        var away = await p.ListByStatusAsync(PresenceStatus.Away);
        Assert.Equal(new[] { "fresh1", "fresh2" }, away.Select(s => s.Player).OrderBy(x => x));
    }

    [Fact]
    public async Task Sweep_reaps_offline_players_and_returns_them()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: Opts());
        var p = sys.Presence;

        await p.HeartbeatAsync("alice");
        await p.HeartbeatAsync("bob");
        clock.Advance(TimeSpan.FromSeconds(90)); // both offline
        await p.HeartbeatAsync("carol");         // still online

        var reaped = await p.SweepAsync();

        Assert.Equal(new[] { "alice", "bob" }, reaped.OrderBy(x => x));
        // carol survives
        Assert.Equal(new[] { "carol" }, (await p.ListOnlineAsync()).Select(s => s.Player));
    }
}
