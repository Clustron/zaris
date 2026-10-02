using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace RateLimiterGateway.Downstream;

/// <summary>
/// The tiny protected service the gateway proxies to. It counts every request it actually serves
/// so the demo can prove the gateway shielded it (served count == globally admitted count, not the
/// total offered load).
/// </summary>
public static class DownstreamApp
{
    public static WebApplication Build(string url)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(url);

        var app = builder.Build();
        long served = 0;

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/api/stats", () => Results.Ok(new { served = Interlocked.Read(ref served) }));

        // The real work endpoint. Every hit here means the gateway admitted the request.
        app.MapMethods("/api/{**rest}", new[] { "GET", "POST", "PUT", "DELETE" }, (HttpContext ctx) =>
        {
            var n = Interlocked.Increment(ref served);
            var instance = ctx.Request.Headers["X-Gateway-Instance"].ToString();
            return Results.Ok(new
            {
                servedByDownstream = true,
                servedCount = n,
                viaGatewayInstance = string.IsNullOrEmpty(instance) ? "direct" : instance,
                path = ctx.Request.Path.Value
            });
        });

        return app;
    }
}
