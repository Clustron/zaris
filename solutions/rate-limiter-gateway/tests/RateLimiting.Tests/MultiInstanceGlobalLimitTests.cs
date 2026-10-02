using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Clustron.Zaris.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace RateLimiterGateway.RateLimiting.Tests;

/// <summary>
/// The headline property: two SEPARATE gateway instances that share one Zaris store enforce ONE
/// global limit. Each instance is a full ASP.NET Core pipeline (TestServer) running the real
/// <see cref="RateLimitingMiddleware"/>; they share a single in-proc <see cref="IZarisClient"/>.
/// If the limit were per-instance, we'd see up to 2x admissions — the test asserts it stays at 1x.
/// </summary>
public sealed class MultiInstanceGlobalLimitTests
{
    private static IHost BuildInstance(IZarisClient shared, string name, Action<RateLimitOptions> configure)
    {
        return new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s => s.AddZarisRateLimiting(shared, configure));
                web.Configure(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        ctx.Response.Headers["X-Gateway-Instance"] = name;
                        await next();
                    });
                    app.UseZarisRateLimiting();
                    // Terminal "downstream": if we got here the limiter admitted the request.
                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status200OK;
                        await ctx.Response.WriteAsync("served");
                    });
                });
            })
            .Build();
    }

    [Fact]
    public async Task Two_instances_sharing_one_store_enforce_one_global_limit()
    {
        const int limit = 100;
        var shared = await TestStore.ConnectUniqueAsync();

        void Configure(RateLimitOptions o)
        {
            o.ApiKeyPolicy = new RateLimitPolicy
            {
                Name = "shared-global",
                Algorithm = RateLimitAlgorithm.SlidingWindowLog,
                Limit = limit,
                Window = TimeSpan.FromSeconds(60) // long window: whole burst falls inside it
            };
        }

        using var gw1 = BuildInstance(shared, "gw-1", Configure);
        using var gw2 = BuildInstance(shared, "gw-2", Configure);
        await gw1.StartAsync();
        await gw2.StartAsync();

        var c1 = gw1.GetTestClient();
        var c2 = gw2.GetTestClient();

        const int offered = 500;
        var tasks = Enumerable.Range(0, offered).Select(async i =>
        {
            var client = (i % 2 == 0) ? c1 : c2; // spread evenly across the two instances
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/thing");
            req.Headers.TryAddWithoutValidation("X-API-Key", "same-key"); // one global bucket
            var resp = await client.SendAsync(req);
            return ((int)resp.StatusCode, resp.Headers.TryGetValues("X-Gateway-Instance", out var v) ? v.First() : "?");
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        var admitted = results.Count(r => r.Item1 == 200);
        var limited = results.Count(r => r.Item1 == 429);
        var admittedOnGw1 = results.Count(r => r.Item1 == 200 && r.Item2 == "gw-1");
        var admittedOnGw2 = results.Count(r => r.Item1 == 200 && r.Item2 == "gw-2");

        // Exactly the global limit admitted — NOT limit-per-instance.
        Assert.Equal(limit, admitted);
        Assert.Equal(offered - limit, limited);
        // And the admissions genuinely came from BOTH instances (proving it's one shared counter).
        Assert.True(admittedOnGw1 > 0 && admittedOnGw2 > 0,
            $"expected admissions from both instances; gw-1={admittedOnGw1}, gw-2={admittedOnGw2}");
        Assert.Equal(limit, admittedOnGw1 + admittedOnGw2);

        await gw1.StopAsync();
        await gw2.StopAsync();
    }

    [Fact]
    public async Task Rejected_requests_carry_retry_after_and_ratelimit_headers()
    {
        var shared = await TestStore.ConnectUniqueAsync();
        void Configure(RateLimitOptions o) => o.ApiKeyPolicy = new RateLimitPolicy
        {
            Name = "hdr", Algorithm = RateLimitAlgorithm.SlidingWindowLog, Limit = 1, Window = TimeSpan.FromSeconds(30)
        };

        using var gw = BuildInstance(shared, "gw-1", Configure);
        await gw.StartAsync();
        var c = gw.GetTestClient();

        void Key(HttpRequestMessage r) => r.Headers.TryAddWithoutValidation("X-API-Key", "hdr-key");

        using var r1 = new HttpRequestMessage(HttpMethod.Get, "/x"); Key(r1);
        var ok = await c.SendAsync(r1);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("1", ok.Headers.GetValues("X-RateLimit-Limit").First());
        Assert.Equal("0", ok.Headers.GetValues("X-RateLimit-Remaining").First());

        using var r2 = new HttpRequestMessage(HttpMethod.Get, "/x"); Key(r2);
        var denied = await c.SendAsync(r2);
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.True(denied.Headers.Contains("Retry-After"));

        await gw.StopAsync();
    }
}
