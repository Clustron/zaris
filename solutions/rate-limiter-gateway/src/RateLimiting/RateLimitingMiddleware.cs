using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RateLimiterGateway.RateLimiting;

/// <summary>
/// ASP.NET Core middleware that enforces a distributed rate limit (backed by the shared Zaris store)
/// before the request is allowed to continue down the pipeline (to the reverse proxy). Emits the
/// standard <c>X-RateLimit-Limit</c> / <c>X-RateLimit-Remaining</c> / <c>X-RateLimit-Reset</c> headers,
/// and on rejection returns <c>429 Too Many Requests</c> with a <c>Retry-After</c> header.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitOptions _options;
    private readonly RateLimitKeyResolver _resolver;
    private readonly IRateLimiterRegistry _registry;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        RateLimitOptions options,
        RateLimitKeyResolver resolver,
        IRateLimiterRegistry registry,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _options = options;
        _resolver = resolver;
        _registry = registry;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!_options.Enabled || _options.IsExcluded(ctx.Request.Path))
        {
            await _next(ctx);
            return;
        }

        var r = _resolver.Resolve(ctx);
        var limiter = _registry.For(r.Policy.Algorithm);

        RateLimitDecision d;
        try
        {
            d = await limiter.AcquireAsync(r.ResourceKey, r.Policy, 1, ctx.RequestAborted);
        }
        catch (ZarisRateLimiterException ex)
        {
            // Fail-open would silently drop the global limit; fail-closed is the safe default for a gateway.
            _logger.LogError(ex, "Rate-limit backend error for {Key}; failing closed.", r.ResourceKey);
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new { error = "rate_limiter_unavailable" });
            return;
        }

        var h = ctx.Response.Headers;
        h["X-RateLimit-Limit"] = d.Limit.ToString(CultureInfo.InvariantCulture);
        h["X-RateLimit-Remaining"] = d.Remaining.ToString(CultureInfo.InvariantCulture);
        h["X-RateLimit-Reset"] = ((long)Math.Ceiling(d.ResetAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        h["X-RateLimit-Policy"] = r.Policy.Name;

        if (!d.Allowed)
        {
            var retrySecs = Math.Max(1, (long)Math.Ceiling(d.RetryAfter.TotalSeconds));
            h["Retry-After"] = retrySecs.ToString(CultureInfo.InvariantCulture);
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "rate_limited",
                policy = r.Policy.Name,
                algorithm = d.Algorithm.ToString(),
                limit = d.Limit,
                retryAfterSeconds = retrySecs
            });
            return;
        }

        await _next(ctx);
    }
}
