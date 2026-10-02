namespace JobQueue.Queue;

/// <summary>Exponential retry backoff with a cap and optional jitter.</summary>
public static class Backoff
{
    /// <summary>
    /// Delay before the next delivery after <paramref name="failedAttempts"/> failures:
    /// <c>Base * 2^(failedAttempts-1)</c>, capped at <paramref name="cap"/>. With jitter, a uniform
    /// 0..100% is applied so a thundering herd of simultaneously-failed jobs spreads out.
    /// </summary>
    public static TimeSpan For(int failedAttempts, TimeSpan baseDelay, TimeSpan cap, bool jitter, Random? rng = null)
    {
        if (failedAttempts < 1) failedAttempts = 1;

        // Guard the shift against overflow for large attempt counts.
        var exp = Math.Min(failedAttempts - 1, 30);
        var scaled = baseDelay.Ticks * (1L << exp);
        if (scaled <= 0 || scaled > cap.Ticks) scaled = cap.Ticks;

        if (jitter)
        {
            var r = rng ?? Random.Shared;
            scaled = (long)(scaled * (0.5 + r.NextDouble() * 0.5)); // 50%..100% of the computed delay
        }
        return TimeSpan.FromTicks(scaled);
    }
}
