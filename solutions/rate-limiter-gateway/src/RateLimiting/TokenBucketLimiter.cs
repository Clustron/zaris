using System;
using System.Threading;
using System.Threading.Tasks;

namespace RateLimiterGateway.RateLimiting;

/// <summary>
/// Token-bucket limiter. Each resource owns a bucket that holds up to <c>Limit</c> tokens (the burst
/// capacity) and refills continuously at <c>RefillPerSecond</c>. A request costing <c>cost</c> tokens is
/// admitted iff the (lazily refilled) bucket holds at least that many, which are then removed.
///
/// State is <c>(tokens, lastRefillUnixMs)</c>. Refill is computed lazily from elapsed wall time on read,
/// so no background timer is needed. The consume step is committed with a Zaris versioned CAS, so under
/// concurrency only one writer per version wins and losers retry — the bucket is never over-drawn.
/// Rejections perform no write (state is unchanged), which keeps the store quiet under heavy throttling.
/// </summary>
public sealed class TokenBucketLimiter : IZarisRateLimiter
{
    private const int MaxCasAttempts = 128;

    private readonly ZarisStateStore _store;
    private readonly IClock _clock;

    public TokenBucketLimiter(ZarisStateStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public RateLimitAlgorithm Algorithm => RateLimitAlgorithm.TokenBucket;

    private sealed record State(double Tokens, long LastMs);

    public async Task<RateLimitDecision> AcquireAsync(
        string resourceKey, RateLimitPolicy policy, int cost = 1, CancellationToken ct = default)
    {
        if (cost < 1) throw new ArgumentOutOfRangeException(nameof(cost));
        var key = $"rl:tb:{resourceKey}";
        var capacity = policy.Limit;
        var rate = policy.RefillPerSecond;
        // A bucket left idle refills to full in capacity/rate seconds; keep it around a little longer.
        var ttl = rate > 0
            ? TimeSpan.FromSeconds(Math.Max(1, (capacity / rate) * 2))
            : TimeSpan.FromMinutes(5);

        for (var attempt = 0; attempt < MaxCasAttempts; attempt++)
        {
            var nowMs = _clock.UtcNow.ToUnixTimeMilliseconds();

            var snap = await _store.ReadAsync<State>(key, ct).ConfigureAwait(false);

            double tokens;
            if (snap.Found && snap.Value is not null)
            {
                var elapsedSec = Math.Max(0, (nowMs - snap.Value.LastMs) / 1000.0);
                tokens = Math.Min(capacity, snap.Value.Tokens + elapsedSec * rate);
            }
            else
            {
                tokens = capacity; // a fresh bucket starts full (allows an initial burst)
            }

            if (tokens >= cost)
            {
                var newState = new State(tokens - cost, nowMs);
                bool committed = snap.Found
                    ? await _store.CompareAndSwapAsync(key, snap.Version, newState, ttl, ct).ConfigureAwait(false)
                    : await _store.CreateIfAbsentAsync(key, newState, ttl, ct).ConfigureAwait(false);

                if (!committed) { await BackoffAsync(attempt, ct).ConfigureAwait(false); continue; }

                var remaining = (long)Math.Floor(newState.Tokens);
                var resetAfter = TimeToRefill(capacity - newState.Tokens, rate);
                return RateLimitDecision.Allow(capacity, remaining, resetAfter, Algorithm, resourceKey);
            }

            // Reject: not enough tokens. No write. Retry-After = time to accrue the shortfall.
            var retryAfter = TimeToRefill(cost - tokens, rate);
            var reset = TimeToRefill(capacity - tokens, rate);
            return RateLimitDecision.Deny(capacity, retryAfter, reset, Algorithm, resourceKey);
        }

        throw new ZarisRateLimiterException(
            $"Token-bucket CAS for '{resourceKey}' did not converge after {MaxCasAttempts} attempts.");
    }

    private static TimeSpan TimeToRefill(double tokensNeeded, double rate)
    {
        if (tokensNeeded <= 0) return TimeSpan.Zero;
        if (rate <= 0) return TimeSpan.MaxValue;
        return TimeSpan.FromSeconds(tokensNeeded / rate);
    }

    private static async Task BackoffAsync(int attempt, CancellationToken ct)
    {
        if (attempt < 4) await Task.Yield();
        else await Task.Delay(Math.Min(5, attempt), ct).ConfigureAwait(false);
    }
}
