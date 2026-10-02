using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using RateLimiterGateway.Downstream;
using RateLimiterGateway.Gateway;
using RateLimiterGateway.LoadDriver;
using RateLimiterGateway.RateLimiting;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// End-to-end demo of a distributed rate limiter + API gateway backed by Clustron Zaris.
//
//   dotnet run                       → self-contained in-process demo:
//                                       starts a downstream + 3 gateway instances that SHARE one
//                                       embedded Zaris store, then fires concurrent load and proves
//                                       the limit holds GLOBALLY across the 3 instances.
//   dotnet run -- --target URL,URL   → fire load at already-running external gateway URLs instead.
// ───────────────────────────────────────────────────────────────────────────────────────────────

string? targetArg = null;
for (var i = 0; i < args.Length; i++)
    if (args[i] == "--target" && i + 1 < args.Length) targetArg = args[i + 1];

using var http = Load.NewClient();

if (targetArg is not null)
{
    var urls = targetArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Console.WriteLine($"Firing load at external gateways: {string.Join(", ", urls)}");
    await RunScenarios(http, urls, downstreamStatsUrl: null);
    return;
}

// ── Self-contained in-process topology ────────────────────────────────────────────────────────
const string downstreamUrl = "http://localhost:5090";
var gatewayUrls = new[] { "http://localhost:5081", "http://localhost:5082", "http://localhost:5083" };
const string zarisConn = "zaris://inproc/gateway-demo";

Console.WriteLine("== Zaris distributed rate limiter — in-process demo ==");
Console.WriteLine($"downstream : {downstreamUrl}");
Console.WriteLine($"gateways   : {string.Join(", ", gatewayUrls)}  (3 instances)");
Console.WriteLine($"zaris store: {zarisConn}  (embedded; all 3 gateways share it)");
Console.WriteLine();

var downstream = DownstreamApp.Build(downstreamUrl);
await downstream.StartAsync();

var sharedClient = await ZarisStoreFactory.ConnectAsync(zarisConn);

void Configure(RateLimitOptions o)
{
    o.IpPolicy = new RateLimitPolicy
    {
        Name = "ip-sliding-window",
        Algorithm = RateLimitAlgorithm.SlidingWindowLog,
        Limit = 20,
        Window = TimeSpan.FromSeconds(10)
    };
    o.ApiKeyPolicy = new RateLimitPolicy
    {
        Name = "apikey-token-bucket",
        Algorithm = RateLimitAlgorithm.TokenBucket,
        Limit = 100,
        RefillPerSecond = 50
    };
}

var gateways = gatewayUrls
    .Select((u, i) => GatewayApp.Build(u, sharedClient, new Uri(downstreamUrl), $"gw-{i + 1}", Configure))
    .ToArray();
foreach (var g in gateways) await g.StartAsync();

// let sockets settle
await Task.Delay(400);

await RunScenarios(http, gatewayUrls, downstreamStatsUrl: $"{downstreamUrl}/api/stats");

foreach (var g in gateways) await g.StopAsync();
await downstream.StopAsync();
return;

// ── Scenarios ───────────────────────────────────────────────────────────────────────────────────
static async Task RunScenarios(HttpClient http, IReadOnlyList<string> gatewayUrls, string? downstreamStatsUrl)
{
    // Scenario A — sliding-window log, per-IP (anonymous). Crisp global cap.
    {
        const int offered = 600, limit = 20;
        Console.WriteLine("── Scenario A: sliding-window log, anonymous (per-IP) ──────────────────");
        Console.WriteLine($"   policy  : {limit} requests / 10s window (GLOBAL, shared by all gateways)");
        Console.WriteLine($"   offered : {offered} concurrent GET /api/ping spread across {gatewayUrls.Count} gateways");

        var r = await Load.RunBurstAsync(http, gatewayUrls, offered, "/api/ping", _ => { });
        PrintResult(r);
        var naive = limit * gatewayUrls.Count;
        Console.WriteLine($"   per-instance limiting would have admitted up to {naive}; Zaris held it to the GLOBAL {limit}.");
        Check("A", r.Admitted == limit, $"admitted == {limit}", r.Admitted);
        Console.WriteLine();
    }

    // Scenario B — token bucket, per API key. Burst = capacity, then sustained refill.
    {
        const int offered = 600, capacity = 100; const double rate = 50;
        var apiKey = "demo-" + Guid.NewGuid().ToString("N")[..8]; // fresh bucket (starts full)
        Console.WriteLine("── Scenario B: token bucket, authenticated (per API key) ───────────────");
        Console.WriteLine($"   policy  : capacity {capacity} tokens, refill {rate}/s (GLOBAL, shared by all gateways)");
        Console.WriteLine($"   offered : {offered} concurrent GET /api/ping with X-API-Key: {apiKey}");

        var r = await Load.RunBurstAsync(http, gatewayUrls, offered, "/api/ping",
            req => req.Headers.TryAddWithoutValidation("X-API-Key", apiKey));
        PrintResult(r);
        // Admissible during the burst: the full bucket (capacity) + whatever refilled while it ran.
        var upper = capacity + (int)Math.Ceiling(rate * r.ElapsedSeconds) + 3;
        Console.WriteLine($"   expected admitted in [{capacity}, {upper}] (capacity + refill during {r.ElapsedSeconds:F3}s burst)");
        Check("B", r.Admitted >= capacity && r.Admitted <= upper, $"{capacity} <= admitted <= {upper}", r.Admitted);
        Console.WriteLine();
    }

    if (downstreamStatsUrl is not null)
    {
        var stats = await http.GetFromJsonAsync<DownstreamStats>(downstreamStatsUrl);
        Console.WriteLine($"Downstream actually served {stats?.Served} requests total (everything else was rejected at the gateway).");
    }
}

static void PrintResult(Load.Result r)
{
    var hist = string.Join(", ", r.StatusHistogram.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}"));
    var byInst = string.Join(", ", r.AdmittedByInstance.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
    Console.WriteLine($"   result  : admitted(200)={r.Admitted}  rateLimited(429)={r.RateLimited}  other={r.Other}  in {r.ElapsedSeconds:F3}s");
    Console.WriteLine($"   status  : [{hist}]");
    Console.WriteLine($"   admitted spread across instances: [{byInst}]  (sum proves ONE global limit, not per-instance)");
}

static void Check(string name, bool ok, string expected, long actual)
    => Console.WriteLine($"   ASSERT {name}: {(ok ? "PASS" : "FAIL")}  (expected {expected}; actual {actual})");

internal sealed record DownstreamStats(long Served);
