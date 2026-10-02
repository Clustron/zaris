using System.Collections.Concurrent;
using LeaderboardPresence.Core;
using Xunit;

namespace LeaderboardPresence.Tests;

public class ConcurrencyIntegrationTests
{
    [Fact]
    public async Task Concurrent_writers_produce_a_correct_leaderboard_and_live_updates()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        const int workers = 32;
        const int perWorker = 50;
        const int players = 16;
        const string board = "battle-royale";

        // Live subscriber counting change events as they arrive.
        var received = 0;
        await using var sub = await lb.SubscribeChangesAsync(_ => { Interlocked.Increment(ref received); return Task.CompletedTask; }, board);
        await Task.Delay(100); // let the subscription settle

        // Oracle: the exact score each player should have if accumulation is linearizable.
        var oracle = new ConcurrentDictionary<string, long>();

        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(async () =>
        {
            for (var i = 0; i < perWorker; i++)
            {
                var player = $"player{(w + i) % players:00}";
                const double delta = 1;
                await lb.SubmitScoreAsync(board, player, delta);
                oracle.AddOrUpdate(player, (long)delta, (_, cur) => cur + (long)delta);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // 1) Every player's score matches the oracle exactly (no lost updates under contention).
        foreach (var kv in oracle)
        {
            var rank = await lb.GetRankAsync(board, kv.Key);
            Assert.NotNull(rank);
            Assert.Equal(kv.Value, (long)rank!.Score);
        }

        // 2) The board is correctly ordered and complete.
        Assert.Equal(players, await lb.GetCountAsync(board));
        var total = (long)workers * perWorker;
        Assert.Equal(total, oracle.Values.Sum());

        var top = await lb.GetTopAsync(board, players);
        Assert.Equal(players, top.Count);
        for (var i = 1; i < top.Count; i++)
            Assert.True(top[i - 1].Score >= top[i].Score, "top list must be in non-increasing score order");
        // top scores equal the oracle's descending scores
        var expectedScoresDesc = oracle.Values.OrderByDescending(v => v).ToArray();
        Assert.Equal(expectedScoresDesc, top.Select(e => (long)e.Score).ToArray());

        // 3) Live updates propagated. Wait for the fan-out to drain.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref received) < total && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        Assert.True(received > 0, "subscriber should have received live change events");
        Assert.Equal(total, Volatile.Read(ref received));
    }

    [Fact]
    public async Task Concurrent_heartbeats_track_all_players_online()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock, pr: new PresenceOptions { OnlineWindow = TimeSpan.FromMinutes(5), AwayWindow = TimeSpan.FromMinutes(10) });
        var p = sys.Presence;

        const int players = 200;
        var tasks = Enumerable.Range(0, players).Select(i => Task.Run(() => p.HeartbeatAsync($"u{i:000}"))).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(players, await p.OnlineCountAsync());
        Assert.Equal(players, (await p.ListOnlineAsync()).Count);
    }
}
