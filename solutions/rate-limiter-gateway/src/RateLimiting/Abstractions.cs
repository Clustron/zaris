using System;
using System.Threading;
using System.Threading.Tasks;

namespace RateLimiterGateway.RateLimiting;

/// <summary>The distributed rate-limiting algorithm a policy uses.</summary>
public enum RateLimitAlgorithm
{
    /// <summary>Sliding-window log: keeps the exact timestamps of requests inside the window.
    /// Gives a precise, no-over-admission cap of <c>Limit</c> requests per <c>Window</c>.</summary>
    SlidingWindowLog,

    /// <summary>Token bucket: a bucket of <c>Limit</c> (burst capacity) tokens that refills at
    /// <c>RefillPerSecond</c>. Allows bursts up to capacity then smooths to the sustained rate.</summary>
    TokenBucket
}

/// <summary>A rate-limiting policy. One policy is applied per resource (an API key or a client IP).</summary>
public sealed record RateLimitPolicy
{
    /// <summary>Human-readable policy name (used only for diagnostics / headers).</summary>
    public string Name { get; init; } = "default";

    public RateLimitAlgorithm Algorithm { get; init; } = RateLimitAlgorithm.SlidingWindowLog;

    /// <summary>Sliding window: max requests allowed inside <see cref="Window"/>.
    /// Token bucket: the bucket capacity (maximum burst).</summary>
    public long Limit { get; init; } = 100;

    /// <summary>Sliding-window length. Ignored by the token bucket.</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Token bucket: sustained refill rate in tokens/second. Ignored by sliding window.</summary>
    public double RefillPerSecond { get; init; } = 10;
}

/// <summary>The outcome of a single rate-limit check, carrying everything needed to emit
/// <c>X-RateLimit-*</c> and <c>Retry-After</c> headers.</summary>
public readonly record struct RateLimitDecision(
    bool Allowed,
    long Limit,
    long Remaining,
    TimeSpan RetryAfter,
    TimeSpan ResetAfter,
    RateLimitAlgorithm Algorithm,
    string ResourceKey)
{
    public static RateLimitDecision Allow(long limit, long remaining, TimeSpan resetAfter, RateLimitAlgorithm algo, string key)
        => new(true, limit, Math.Max(0, remaining), TimeSpan.Zero, resetAfter, algo, key);

    public static RateLimitDecision Deny(long limit, TimeSpan retryAfter, TimeSpan resetAfter, RateLimitAlgorithm algo, string key)
        => new(false, limit, 0, retryAfter, resetAfter, algo, key);
}

/// <summary>A distributed rate limiter backed by a shared Zaris store. Implementations are safe
/// to call concurrently from many threads and many processes/instances against one store.</summary>
public interface IZarisRateLimiter
{
    RateLimitAlgorithm Algorithm { get; }

    /// <summary>Atomically attempt to admit <paramref name="cost"/> units against
    /// <paramref name="resourceKey"/> under <paramref name="policy"/>.</summary>
    Task<RateLimitDecision> AcquireAsync(
        string resourceKey, RateLimitPolicy policy, int cost = 1, CancellationToken ct = default);
}

/// <summary>Thrown when the backing store returns a terminal (non-retryable) failure.</summary>
public sealed class ZarisRateLimiterException : Exception
{
    public ZarisRateLimiterException(string message) : base(message) { }
}
