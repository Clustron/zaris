using System.Diagnostics;
using LeaderboardPresence.Api;
using LeaderboardPresence.Api.Simulation;
using LeaderboardPresence.Core;

// Two modes:
//   dotnet run                -> run the end-to-end simulation (boots server + live client + load), prints evidence, exits
//   dotnet run -- serve [url] -> run the HTTP API + SignalR server and block (for manual exploration / real clients)
var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "simulate";

if (mode == "serve")
{
    var url = args.ElementAtOrDefault(1) ?? "http://127.0.0.1:5080";
    var app = await AppBuilder.BuildAsync(url);
    Console.WriteLine($"Leaderboard + Presence API listening on {url}");
    Console.WriteLine($"  POST {url}/api/boards/global-arena/scores        {{ \"player\": \"alice\", \"delta\": 100 }}");
    Console.WriteLine($"  GET  {url}/api/boards/global-arena/top?count=10");
    Console.WriteLine($"  POST {url}/api/presence/alice/heartbeat");
    Console.WriteLine($"  GET  {url}/api/presence/online");
    Console.WriteLine($"  WS   {url}/hub/live   (SignalR: JoinBoard / JoinPresence -> leaderboardChanged / presenceChanged)");
    await app.RunAsync();
}
else if (mode == "bench")
{
    // Pure Zaris core-path throughput (no HTTP, no SignalR): isolates how fast the leaderboard itself sustains
    // concurrent score submissions. Each submit = atomic increments across all-time+daily+weekly + a rank read.
    var total = int.TryParse(args.ElementAtOrDefault(1), out var t) ? t : 50_000;
    var concurrency = int.TryParse(args.ElementAtOrDefault(2), out var c) ? c : 64;
    var players = 1_000;
    var publish = args.ElementAtOrDefault(3) == "publish";
    await using var sys = await LeaderboardPresenceSystem.ConnectAsync(
        "zaris://inproc/bench-" + Guid.NewGuid().ToString("N"),
        new LeaderboardOptions { PublishChanges = publish, AuditStreamMaxLen = 1000 });
    Console.WriteLine($"[bench] publishChanges={publish}");
    Console.WriteLine($"[bench] {total:N0} SubmitScoreAsync across {concurrency} workers, {players} players, 3 windows/submit...");
    var next = -1;
    var sw = Stopwatch.StartNew();
    await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
    {
        var rnd = new Random(Guid.NewGuid().GetHashCode());
        int i;
        while ((i = Interlocked.Increment(ref next)) < total)
            await sys.Leaderboard.SubmitScoreAsync("bench", $"player{rnd.Next(players):0000}", rnd.Next(1, 11));
    })));
    sw.Stop();
    Console.WriteLine($"[bench] {total:N0} submits in {sw.Elapsed.TotalSeconds:F2}s = {total / sw.Elapsed.TotalSeconds:N0} submits/sec");
    Console.WriteLine($"[bench] (each submit performs 3 sorted-set increments + 1 rank query = {4L * total / sw.Elapsed.TotalSeconds:N0} sorted-set ops/sec)");
}
else
{
    await Simulator.RunAsync(args);
}
