using System;
using Clustron.Zaris.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace RateLimiterGateway.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the distributed rate-limiting services against an already-connected shared Zaris client.
    /// Both algorithm implementations are registered and selected per-policy at request time.
    /// </summary>
    public static IServiceCollection AddZarisRateLimiting(
        this IServiceCollection services,
        IZarisClient sharedClient,
        Action<RateLimitOptions>? configure = null)
    {
        var options = new RateLimitOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(sharedClient);
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton<ZarisStateStore>();
        services.AddSingleton<RateLimitKeyResolver>();

        services.AddSingleton<IZarisRateLimiter, SlidingWindowLogLimiter>();
        services.AddSingleton<IZarisRateLimiter, TokenBucketLimiter>();
        services.AddSingleton<IRateLimiterRegistry, RateLimiterRegistry>();

        return services;
    }

    /// <summary>Registers the reverse-proxy forwarder target + its HttpClient.</summary>
    public static IServiceCollection AddReverseProxy(this IServiceCollection services, Uri downstreamBaseAddress)
    {
        services.AddSingleton(new ReverseProxyOptions { DownstreamBaseAddress = downstreamBaseAddress });
        services.AddHttpClient("downstream")
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });
        return services;
    }
}

public static class RateLimitingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseZarisRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimitingMiddleware>();

    public static IApplicationBuilder UseReverseProxy(this IApplicationBuilder app)
        => app.UseMiddleware<ReverseProxyMiddleware>();
}
