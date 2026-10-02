using RateLimiterGateway.Downstream;

// Standalone downstream host. URL via arg or DOWNSTREAM_URL env, default http://localhost:5090.
var url = args.Length > 0 ? args[0]
    : Environment.GetEnvironmentVariable("DOWNSTREAM_URL") ?? "http://localhost:5090";

var app = DownstreamApp.Build(url);
Console.WriteLine($"[downstream] listening on {url}");
await app.RunAsync();
