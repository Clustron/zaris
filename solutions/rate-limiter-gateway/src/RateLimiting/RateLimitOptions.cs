using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace RateLimiterGateway.RateLimiting;

/// <summary>Gateway-wide rate-limiting configuration.</summary>
public sealed class RateLimitOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Header carrying the caller's API key. When present, the per-key policy applies;
    /// otherwise the request is limited per client IP.</summary>
    public string ApiKeyHeader { get; set; } = "X-API-Key";

    /// <summary>Default policy applied to any request that presents an API key.</summary>
    public RateLimitPolicy ApiKeyPolicy { get; set; } = new()
    {
        Name = "api-key-default",
        Algorithm = RateLimitAlgorithm.TokenBucket,
        Limit = 100,
        RefillPerSecond = 50
    };

    /// <summary>Policy applied to anonymous (no API key) requests, keyed by client IP.</summary>
    public RateLimitPolicy IpPolicy { get; set; } = new()
    {
        Name = "ip-default",
        Algorithm = RateLimitAlgorithm.SlidingWindowLog,
        Limit = 20,
        Window = TimeSpan.FromSeconds(10)
    };

    /// <summary>Optional per-API-key overrides (e.g. a premium key with a bigger quota).</summary>
    public Dictionary<string, RateLimitPolicy> PerApiKeyPolicies { get; } = new();

    /// <summary>Path prefixes exempt from limiting (health checks, etc.).</summary>
    public List<string> ExcludedPathPrefixes { get; } = new() { "/healthz", "/_gateway" };

    public bool IsExcluded(PathString path)
    {
        foreach (var p in ExcludedPathPrefixes)
            if (path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
