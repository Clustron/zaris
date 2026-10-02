namespace JobQueue.Queue;

/// <summary>The lifecycle state of a job. The job document is the authoritative source of truth.</summary>
public enum JobState
{
    /// <summary>Enqueued and waiting to be claimed (may still be in the future if scheduled/backed off).</summary>
    Ready = 0,

    /// <summary>Claimed by a worker under a visibility lease. Reverts to <see cref="Ready"/> if the lease expires.</summary>
    Claimed = 1,

    /// <summary>Acked by the owning worker. Terminal.</summary>
    Completed = 2,

    /// <summary>Exhausted all delivery attempts (or crashed too many times). Terminal; parked in the DLQ.</summary>
    DeadLettered = 3,
}

/// <summary>
/// The durable job document, stored at <c>job:{queue}:{id}</c> and mutated only through versioned CAS.
/// Every field that drives a scheduling or correctness decision lives here so the decision survives a
/// worker crash: visibility (<see cref="VisibleAtUtc"/>), the lease (<see cref="LeaseOwner"/> +
/// <see cref="LeaseExpiresUtc"/>), the delivery count (<see cref="Attempts"/>) and the cap
/// (<see cref="MaxAttempts"/>).
/// </summary>
public sealed class Job
{
    public string Id { get; set; } = "";
    public string Queue { get; set; } = "";
    public string Payload { get; set; } = "";

    /// <summary>Higher runs first. Ties broken by <see cref="VisibleAtUtc"/> then <see cref="EnqueuedUtc"/>.</summary>
    public int Priority { get; set; }

    public JobState State { get; set; } = JobState.Ready;

    /// <summary>The job is invisible (not claimable) until this instant. Drives delay/schedule and backoff.</summary>
    public DateTimeOffset VisibleAtUtc { get; set; }

    public DateTimeOffset EnqueuedUtc { get; set; }

    /// <summary>Number of times the job has been delivered to a worker (claims + reclaims). Bounded by <see cref="MaxAttempts"/>.</summary>
    public int Attempts { get; set; }

    public int MaxAttempts { get; set; } = 3;

    /// <summary>The worker that currently holds the lease (null when not claimed).</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>When the current lease expires. After this instant the job is reclaimable by any worker.</summary>
    public DateTimeOffset LeaseExpiresUtc { get; set; }

    public string? LastError { get; set; }
    public DateTimeOffset? LastFinishedUtc { get; set; }

    public Job Clone() => (Job)MemberwiseClone();
}

/// <summary>A compact view of a job for the demo/monitoring output (no behaviour).</summary>
public readonly record struct JobView(
    string Id, JobState State, int Attempts, int MaxAttempts, string? LeaseOwner, string? LastError);
