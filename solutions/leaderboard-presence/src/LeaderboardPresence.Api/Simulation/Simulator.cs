using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using LeaderboardPresence.Core;
using Microsoft.AspNetCore.SignalR.Client;

namespace LeaderboardPresence.Api.Simulation;

/// <summary>
/// End-to-end demonstration: boots the real HTTP + SignalR server (backed by embedded Zaris), connects a live
/// SignalR client, then drives many concurrent simulated players submitting scores and heart-beating through the
/// REST API. It proves the top-N stays correct under contention (vs a local oracle) and that updates propagate live.
/// </summary>
public static class Simulator
{
    private const string Board = "global-arena";

    public static async Task RunAsync(string[] args)
    {
        var url = "http://127.0.0.1:5080";
        const int players = 400;
        const int totalSubmits = 4_000;
        const int concurrency = 40;

        Console.WriteLine("================ Zaris Leaderboard + Presence — End-to-End Simulation ================");
        Console.WriteLine($"server         : {url}");
        Console.WriteLine($"players        : {players}");
        Console.WriteLine($"score submits  : {totalSubmits:N0} across {concurrency} concurrent workers");
        Console.WriteLine($"transport      : HTTP REST -> Zaris sorted set/pub-sub -> SignalR live push");
        Console.WriteLine("=====================================================================================");

        // Short presence windows so decay (online->away->offline) is visible within the demo.
        var presence = new PresenceOptions
        {
            OnlineWindow = TimeSpan.FromSeconds(3),
            AwayWindow = TimeSpan.FromSeconds(6),
            TtlGrace = TimeSpan.FromSeconds(5)
        };

        await using var app = await AppBuilder.BuildAsync(url, "zaris://inproc/sim", presence, sweepInterval: TimeSpan.FromSeconds(2));
        await app.StartAsync();
        Console.WriteLine("[server] started.");

        // ── live SignalR client ──
        var lbEvents = 0;
        var presenceEvents = 0;
        LeaderboardChange? lastChange = null;
        var conn = new HubConnectionBuilder().WithUrl($"{url}/hub/live").Build();
        conn.On<JsonElement>("leaderboardChanged", e =>
        {
            Interlocked.Increment(ref lbEvents);
            try
            {
                lastChange = new LeaderboardChange(
                    e.GetProperty("board").GetString()!, e.GetProperty("window").GetString()!,
                    e.GetProperty("player").GetString()!, e.GetProperty("newScore").GetDouble(),
                    e.GetProperty("newRank").GetInt64(), e.GetProperty("delta").GetDouble(),
                    e.GetProperty("timestampUtc").GetDateTimeOffset());
            }
            catch { /* display only */ }
        });
        conn.On<JsonElement>("presenceChanged", _ => Interlocked.Increment(ref presenceEvents));
        await conn.StartAsync();
        await conn.InvokeAsync("JoinBoard", Board);
        await conn.InvokeAsync("JoinPresence");
        Console.WriteLine("[client] SignalR connected and subscribed to board + presence.\n");

        var http = new HttpClient { BaseAddress = new Uri(url) };

        // ── Phase 1: everyone comes online ──
        Console.WriteLine($"[phase 1] {players} players sending heartbeats (coming online)...");
        await ParallelForAsync(players, concurrency, async i =>
        {
            await http.PostAsJsonAsync($"/api/presence/player{i:000}/heartbeat", new AppBuilder.HeartbeatRequest($"device-{i % 5}"));
        });
        var onlineAfterJoin = await http.GetFromJsonAsync<OnlineCount>("/api/presence/count");
        Console.WriteLine($"[phase 1] online now = {onlineAfterJoin!.Online}\n");

        // ── Phase 2: concurrent score storm ──
        Console.WriteLine($"[phase 2] {totalSubmits:N0} concurrent score submissions...");
        var oracle = new ConcurrentDictionary<string, long>();
        var ok = 0;
        var rnd = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
        var sw = Stopwatch.StartNew();
        await ParallelForAsync(totalSubmits, concurrency, async _ =>
        {
            var r = rnd.Value!;
            var player = $"player{r.Next(players):000}";
            long delta = r.Next(1, 11);
            var resp = await http.PostAsJsonAsync($"/api/boards/{Board}/scores", new AppBuilder.ScoreRequest(player, delta));
            if (resp.IsSuccessStatusCode)
            {
                oracle.AddOrUpdate(player, delta, (_, cur) => cur + delta);
                Interlocked.Increment(ref ok);
            }
        });
        sw.Stop();
        var rate = ok / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"[phase 2] {ok:N0} submits OK in {sw.Elapsed.TotalSeconds:F2}s = {rate:N0} ops/sec\n");

        // ── Phase 3: verify correctness against the oracle ──
        Console.WriteLine("[phase 3] verifying leaderboard correctness against local oracle...");
        var top = (await http.GetFromJsonAsync<List<RankedEntry>>($"/api/boards/{Board}/top?count={players}"))!;
        var mismatches = 0;
        foreach (var e in top)
            if (!oracle.TryGetValue(e.Player, out var expected) || expected != (long)e.Score)
                mismatches++;
        var expectedTotal = oracle.Values.Sum();
        var actualTotal = top.Sum(e => (long)e.Score);
        var ordered = top.Zip(top.Skip(1)).All(p => p.First.Score >= p.Second.Score);

        Console.WriteLine($"[phase 3] players on board      : {top.Count} (expected {oracle.Count})");
        Console.WriteLine($"[phase 3] score sum (server)    : {actualTotal:N0}");
        Console.WriteLine($"[phase 3] score sum (oracle)    : {expectedTotal:N0}");
        Console.WriteLine($"[phase 3] per-player mismatches : {mismatches}");
        Console.WriteLine($"[phase 3] strictly ordered      : {ordered}");
        Console.WriteLine($"[phase 3] RESULT                : {(mismatches == 0 && actualTotal == expectedTotal && ordered ? "CORRECT ✓" : "FAILED ✗")}\n");

        Console.WriteLine("---- TOP 15 (all-time) ----");
        Console.WriteLine($"{"rank",4}  {"player",-12}  {"score",8}");
        foreach (var e in top.Take(15))
            Console.WriteLine($"{e.Rank,4}  {e.Player,-12}  {e.Score,8:N0}");
        Console.WriteLine();

        // neighbors demo around a mid-rank player
        var mid = top[top.Count / 2].Player;
        var neighbors = (await http.GetFromJsonAsync<List<RankedEntry>>($"/api/boards/{Board}/players/{mid}/neighbors?radius=3"))!;
        Console.WriteLine($"---- neighbors of {mid} (rank {neighbors.FirstOrDefault(n => n.Player == mid)?.Rank}) ----");
        foreach (var e in neighbors) Console.WriteLine($"  #{e.Rank,-4} {e.Player,-12} {e.Score,8:N0}{(e.Player == mid ? "   <-- you" : "")}");
        Console.WriteLine();

        // ── Phase 4: presence decay ──
        Console.WriteLine("[phase 4] presence decay: keeping 50 players alive, letting the rest go idle...");
        for (var round = 0; round < 3; round++)
        {
            await ParallelForAsync(50, 25, async i => await http.PostAsJsonAsync($"/api/presence/player{i:000}/heartbeat", new AppBuilder.HeartbeatRequest("active")));
            await Task.Delay(2000);
            var online = (await http.GetFromJsonAsync<OnlineCount>("/api/presence/count"))!.Online;
            var sample = await http.GetFromJsonAsync<PresenceSnapshot>("/api/presence/player399");
            Console.WriteLine($"   t+{(round + 1) * 2}s  online={online,4}   player399 (idle) = {sample!.Status}");
        }
        Console.WriteLine();

        // let live events drain (async fan-out via the bridge's dispatch queue)
        var drainDeadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref lbEvents) < ok && DateTime.UtcNow < drainDeadline)
            await Task.Delay(50);
        Console.WriteLine("---- live push (SignalR) ----");
        Console.WriteLine($"leaderboard events received by live client : {Volatile.Read(ref lbEvents):N0} (of {ok:N0} submits)");
        Console.WriteLine($"presence events received by live client    : {Volatile.Read(ref presenceEvents):N0}");
        if (lastChange is not null)
            Console.WriteLine($"last change seen live                      : {lastChange.Player} -> score {lastChange.NewScore:N0}, rank {lastChange.NewRank}");
        Console.WriteLine();

        // ── audit stream replay ──
        var audit = (await http.GetFromJsonAsync<List<LeaderboardChange>>($"/api/boards/{Board}/audit?count=5"))!;
        Console.WriteLine("---- durable audit stream (newest 5, replayed from Zaris stream) ----");
        foreach (var a in audit) Console.WriteLine($"  {a.TimestampUtc:HH:mm:ss.fff}  {a.Player,-12} +{a.Delta,-3:N0} -> {a.NewScore,6:N0} (rank {a.NewRank})");

        Console.WriteLine("\n================ Simulation complete ================");

        await conn.DisposeAsync();
        http.Dispose();
        await app.StopAsync();
    }

    private sealed record OnlineCount(long Online);

    private static async Task ParallelForAsync(int count, int concurrency, Func<int, Task> body)
    {
        var next = -1;
        var workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            int i;
            while ((i = Interlocked.Increment(ref next)) < count)
                await body(i);
        }));
        await Task.WhenAll(workers);
    }
}
