using Microsoft.AspNetCore.Http;

namespace RateLimiterGateway.RateLimiting;

/// <summary>Maps an incoming request to the resource key + policy that governs it.</summary>
public sealed class RateLimitKeyResolver
{
    private readonly RateLimitOptions _options;

    public RateLimitKeyResolver(RateLimitOptions options) => _options = options;

    public readonly record struct Resolution(string ResourceKey, RateLimitPolicy Policy, string Kind, string Identity);

    public Resolution Resolve(HttpContext ctx)
    {
        var apiKey = ctx.Request.Headers[_options.ApiKeyHeader].ToString();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var policy = _options.PerApiKeyPolicies.TryGetValue(apiKey, out var custom)
                ? custom
                : _options.ApiKeyPolicy;
            return new Resolution($"key:{apiKey}", policy, "api-key", apiKey);
        }

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return new Resolution($"ip:{ip}", _options.IpPolicy, "ip", ip);
    }
}
