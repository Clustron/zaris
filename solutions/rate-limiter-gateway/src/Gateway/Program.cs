using System;
using System.Linq;
using System.Threading.Tasks;
using RateLimiterGateway.Gateway;
using RateLimiterGateway.RateLimiting;

// Standalone gateway host. Starts one or more gateway instances (comma-separated URLs), all sharing
// ONE Zaris store connection, so the rate limit is global across them.
//
// Env/args:
//   GATEWAY_URLS   = http://localhost:5081,http://localhost:5082,http://localhost:5083
//   ZARIS_CONN     = zaris://inproc/gateway   (or zaris://host:port/store for a networked node)
//   DOWNSTREAM_URL = http://localhost:5090

var urls = (Environment.GetEnvironmentVariable("GATEWAY_URLS")
            ?? (args.Length > 0 ? args[0] : "http://localhost:5081,http://localhost:5082,http://localhost:5083"))
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var zarisConn = Environment.GetEnvironmentVariable("ZARIS_CONN") ?? "zaris://inproc/gateway";
var downstream = new Uri(Environment.GetEnvironmentVariable("DOWNSTREAM_URL") ?? "http://localhost:5090");

Console.WriteLine($"[gateway] connecting shared Zaris store: {zarisConn}");
var sharedClient = await ZarisStoreFactory.ConnectAsync(zarisConn);
Console.WriteLine($"[gateway] connected. downstream={downstream}");

void Configure(RateLimitOptions o)
{
    // Demo policy: anonymous (per-IP) sliding window; API-key (token bucket) with a premium override.
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
        Limit = 100,            // burst capacity
        RefillPerSecond = 50    // sustained rate
    };
    o.PerApiKeyPolicies["premium-key"] = new RateLimitPolicy
    {
        Name = "premium-token-bucket",
        Algorithm = RateLimitAlgorithm.TokenBucket,
        Limit = 1000,
        RefillPerSecond = 500
    };
}

var apps = urls.Select((u, i) =>
    GatewayApp.Build(u, sharedClient, downstream, $"gw-{i + 1}", Configure)).ToArray();

foreach (var app in apps)
{
    await app.StartAsync();
    Console.WriteLine($"[gateway] instance listening on {string.Join(",", app.Urls)}");
}

Console.WriteLine($"[gateway] {apps.Length} instance(s) up, all sharing {zarisConn}. Ctrl+C to stop.");
await Task.WhenAll(apps.Select(a => a.WaitForShutdownAsync()));
