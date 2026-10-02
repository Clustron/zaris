using System;
using Clustron.Zaris.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RateLimiterGateway.RateLimiting;

namespace RateLimiterGateway.Gateway;

/// <summary>
/// Builds one gateway instance: a Kestrel host whose pipeline is
/// [distributed rate limiter] -> [reverse proxy to downstream]. Several instances built with the
/// same <paramref name="sharedClient"/> enforce one global limit because they share the Zaris store.
/// </summary>
public static class GatewayApp
{
    public static WebApplication Build(
        string url,
        IZarisClient sharedClient,
        Uri downstreamBaseAddress,
        string instanceName,
        Action<RateLimitOptions>? configureLimits = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(url);

        builder.Services.AddZarisRateLimiting(sharedClient, configureLimits);
        builder.Services.AddReverseProxy(downstreamBaseAddress);

        var app = builder.Build();

        // Pure middleware pipeline (no endpoint routing) so the terminal reverse proxy is genuinely last:
        //   stamp instance + handle health  ->  distributed rate limiter  ->  reverse proxy.
        app.Use(async (ctx, next) =>
        {
            ctx.Request.Headers["X-Gateway-Instance"] = instanceName;   // forwarded to downstream
            ctx.Response.Headers["X-Gateway-Instance"] = instanceName;  // visible to the client

            if (ctx.Request.Path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Response.WriteAsJsonAsync(new { status = "ok", instance = instanceName });
                return;
            }
            await next();
        });

        app.UseZarisRateLimiting();
        app.UseReverseProxy();

        return app;
    }
}
