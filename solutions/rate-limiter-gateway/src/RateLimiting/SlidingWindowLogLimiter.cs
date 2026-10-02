using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RateLimiterGateway.RateLimiting;

/// <summary>
/// Sliding-window log limiter. For each resource it keeps the exact unix-ms timestamps of the
/// requests admitted inside the current window. On each request it prunes timestamps older than
/// <c>now - Window</c>, and admits iff the surviving count is below <c>Limit</c>.
///
/// Correctness: the read-prune-append-write cycle is performed under a Zaris versioned CAS, so under
/// concurrency exactly one writer per version commits; losers retry against the winner's state. This
/// gives a precise global cap — never more than <c>Limit</c> admissions per window, no matter how many
/// gateway instances or threads race — which is the property the demo and tests assert.
/// </summary>
public sealed class SlidingWindowLogLimiter : IZarisRateLimiter
{
    private const int MaxCasAttempts = 128;

    private readonly ZarisStateStore _store;
    private readonly IClock _clock;

    public SlidingWindowLogLimiter(ZarisStateStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public RateLimitAlgorithm Algorithm => RateLimitAlgorithm.SlidingWindowLog;

    // Serialized state: the list of admit timestamps (unix ms) currently inside the window.
    private sealed record State(List<long> T);

    public async Task<RateLimitDecision> AcquireAsync(
        string resourceKey, RateLimitPolicy policy, int cost = 1, CancellationToken ct = default)
    {
        if (cost < 1) throw new ArgumentOutOfRangeException(nameof(cost));
        var key = $"rl:sw:{resourceKey}";
        var windowMs = (long)policy.Window.TotalMilliseconds;

        for (var attempt = 0; attempt < MaxCasAttempts; attempt++)
        {
            var nowMs = _clock.UtcNow.ToUnixTimeMilliseconds();
            var cutoff = nowMs - windowMs;

            var snap = await _store.ReadAsync<State>(key, ct).ConfigureAwait(false);
            var kept = snap.Found && snap.Value is not null
                ? snap.Value.T.Where(t => t > cutoff).ToList()
                : new List<long>();

            if (kept.Count + cost <= policy.Limit)
            {
                // Admit: append this request's timestamp(s).
                for (var i = 0; i < cost; i++) kept.Add(nowMs);
                kept.Sort();

                bool committed = snap.Found
                    ? await _store.CompareAndSwapAsync(key, snap.Version, new State(kept), policy.Window, ct).ConfigureAwait(false)
                    : await _store.CreateIfAbsentAsync(key, new State(kept), policy.Window, ct).ConfigureAwait(false);

                if (!committed) { await BackoffAsync(attempt, ct).ConfigureAwait(false); continue; }

                var remaining = policy.Limit - kept.Count;
                // Window resets (frees a slot) when the oldest kept timestamp exits the window.
                var resetAfter = TimeSpan.FromMilliseconds(Math.Max(0, kept[0] + windowMs - nowMs));
                return RateLimitDecision.Allow(policy.Limit, remaining, resetAfter, Algorithm, resourceKey);
            }

            // Reject: no write needed (state is unchanged). Retry-After = until the oldest in-window
            // request ages out, freeing a slot.
            var retryAfter = kept.Count > 0
                ? TimeSpan.FromMilliseconds(Math.Max(1, kept[0] + windowMs - nowMs))
                : policy.Window;
            return RateLimitDecision.Deny(policy.Limit, retryAfter, retryAfter, Algorithm, resourceKey);
        }

        throw new ZarisRateLimiterException(
            $"Sliding-window CAS for '{resourceKey}' did not converge after {MaxCasAttempts} attempts.");
    }

    private static async Task BackoffAsync(int attempt, CancellationToken ct)
    {
        if (attempt < 4) await Task.Yield();
        else await Task.Delay(Math.Min(5, attempt), ct).ConfigureAwait(false);
    }
}
