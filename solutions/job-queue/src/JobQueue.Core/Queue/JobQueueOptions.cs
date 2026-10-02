namespace JobQueue.Queue;

/// <summary>
/// Tunables for a queue. Defaults are production-sane; tests dial the timings down so lease expiry and
/// backoff happen in milliseconds.
/// </summary>
public sealed class JobQueueOptions
{
    /// <summary>How long a claim holds a job exclusively before it becomes reclaimable. Default 30s.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Default max deliveries before a job is dead-lettered. Overridable per enqueue.</summary>
    public int DefaultMaxAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential retry backoff (delay = Base * 2^(attempt-1), capped).</summary>
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Ceiling for retry backoff.</summary>
    public TimeSpan RetryBackoffCap { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Deterministic jitter is off by default so tests are reproducible; the demo turns it on.</summary>
    public bool UseBackoffJitter { get; set; }

    /// <summary>
    /// How long a finished (Completed) job document is retained before Zaris reclaims it via write-time
    /// TTL. Keeps recently-finished jobs queryable for idempotency/auditing, then self-cleans.
    /// </summary>
    public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromMinutes(10);
}
