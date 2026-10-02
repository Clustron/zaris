using JobQueue.Infrastructure;

namespace JobQueue.Queue;

/// <summary>The result of a worker reporting failure of a delivery.</summary>
public enum FailKind
{
    /// <summary>Scheduled for another delivery after a backoff delay.</summary>
    Retried,
    /// <summary>Delivery budget exhausted — moved to the dead-letter queue.</summary>
    DeadLettered,
    /// <summary>The caller no longer owned the lease (it had expired and been reclaimed, or the job was terminal).</summary>
    LostLease,
}

/// <summary>Outcome of <see cref="JobQueueClient.FailAsync"/>.</summary>
public readonly record struct FailResult(FailKind Kind, DateTimeOffset? NextVisibleUtc, int Attempts);

/// <summary>
/// The queue engine: a thread-safe facade over the Zaris document store that producers and workers
/// share. It owns every state transition and expresses each one as a single compare-and-swap on the
/// durable job document, which is what gives the queue its guarantees:
///
/// <list type="bullet">
///   <item><b>Single-winner claim</b> — the Ready→Claimed flip is one CAS; the monotonic document
///         version serialises concurrent claimers so exactly one wins, the rest get a conflict.</item>
///   <item><b>Lease-expiry requeue</b> — a crashed worker's job sits in Claimed with an expired
///         <see cref="Job.LeaseExpiresUtc"/>; that is simply another <i>claimable</i> condition in the
///         same predicate, so the next claimer takes it over with one CAS. No background sweeper is
///         required for correctness.</item>
///   <item><b>At-least-once with a cap</b> — every (re)claim increments <see cref="Job.Attempts"/>;
///         once it reaches <see cref="Job.MaxAttempts"/> the job is dead-lettered instead of
///         redelivered.</item>
/// </list>
/// </summary>
public sealed class JobQueueClient
{
    private readonly ZarisDocumentStore _store;
    private readonly QueueKeys _keys;
    private readonly JobQueueOptions _options;
    private readonly IClock _clock;
    private readonly IndexSet _active;
    private readonly IndexSet _dlq;

    public JobQueueClient(ZarisDocumentStore store, string queue, JobQueueOptions options, IClock clock)
    {
        _store = store;
        _keys = new QueueKeys(queue);
        _options = options;
        _clock = clock;
        _active = new IndexSet(store, _keys.ActiveIndex);
        _dlq = new IndexSet(store, _keys.DeadLetterIndex);
    }

    public string Queue => _keys.Queue;
    public JobQueueOptions Options => _options;

    // ----------------------------------------------------------------- enqueue

    /// <summary>
    /// Enqueues a job. Optionally <paramref name="delay"/> or <paramref name="scheduledFor"/> keep it
    /// invisible until due, and a higher <paramref name="priority"/> runs sooner.
    ///
    /// Idempotent enqueue: pass a stable <paramref name="jobId"/> (e.g. a business key). The job
    /// document is created with <c>IfAbsent</c>, so re-enqueuing the same id is a no-op on the payload.
    /// The write order is job-document-first (the authoritative record) then index-add, and both the
    /// create and the index add are idempotent, so a retried enqueue heals a half-finished one without
    /// ever producing a duplicate job.
    /// </summary>
    public async Task<string> EnqueueAsync(
        string payload,
        int priority = 0,
        TimeSpan? delay = null,
        DateTimeOffset? scheduledFor = null,
        int? maxAttempts = null,
        string? jobId = null,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var id = jobId ?? Guid.NewGuid().ToString("N");
        var visibleAt = scheduledFor ?? (delay is { } d ? now + d : now);

        var job = new Job
        {
            Id = id,
            Queue = _keys.Queue,
            Payload = payload,
            Priority = priority,
            State = JobState.Ready,
            VisibleAtUtc = visibleAt,
            EnqueuedUtc = now,
            Attempts = 0,
            MaxAttempts = maxAttempts ?? _options.DefaultMaxAttempts,
        };

        // Authoritative record first (IfAbsent dedupes on id), then the discovery index (idempotent).
        await _store.CreateAsync(_keys.Job(id), job, ct: ct).ConfigureAwait(false);
        await _active.AddAsync(id, ct).ConfigureAwait(false);
        return id;
    }

    // ----------------------------------------------------------------- claim

    /// <summary>
    /// Attempts to claim the single best due-and-claimable job for <paramref name="workerId"/>. Returns
    /// the claimed job (now owned under a fresh lease) or <c>null</c> if nothing is claimable right now.
    ///
    /// Discovery uses the active index (there is no prefix scan); the authoritative claim is the CAS on
    /// the chosen job document. A job whose crashed owner already used the whole attempt budget is
    /// dead-lettered here instead of being redelivered.
    /// </summary>
    public async Task<Job?> TryClaimAsync(string workerId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var ids = await _active.ListAsync(ct).ConfigureAwait(false);

        // Read candidates with their CAS versions, dropping terminal/missing ones (and healing the index).
        var candidates = new List<(Job Job, long Version)>();
        foreach (var id in ids)
        {
            var doc = await _store.ReadAsync<Job>(_keys.Job(id), ct).ConfigureAwait(false);
            if (!doc.Found || doc.Value is null)
            {
                await _active.RemoveAsync(id, ct).ConfigureAwait(false); // stale pointer — heal.
                continue;
            }
            var job = doc.Value;
            if (job.State is JobState.Completed or JobState.DeadLettered)
            {
                await _active.RemoveAsync(id, ct).ConfigureAwait(false);
                continue;
            }
            if (IsClaimable(job, now))
                candidates.Add((job, doc.Version));
        }

        // Best first: priority desc, then earliest-due, then oldest.
        candidates.Sort((a, b) =>
        {
            var c = b.Job.Priority.CompareTo(a.Job.Priority);
            if (c != 0) return c;
            c = a.Job.VisibleAtUtc.CompareTo(b.Job.VisibleAtUtc);
            if (c != 0) return c;
            return a.Job.EnqueuedUtc.CompareTo(b.Job.EnqueuedUtc);
        });

        foreach (var (job, version) in candidates)
        {
            // A crashed owner that already used the whole budget: retire it rather than redeliver.
            if (job.Attempts >= job.MaxAttempts)
            {
                await TryDeadLetterStaleAsync(job, version, now, ct).ConfigureAwait(false);
                continue;
            }

            var claimed = job.Clone();
            claimed.State = JobState.Claimed;
            claimed.Attempts = job.Attempts + 1;
            claimed.LeaseOwner = workerId;
            claimed.LeaseExpiresUtc = now + _options.LeaseDuration;

            if (await _store.CompareAndSwapAsync(_keys.Job(job.Id), version, claimed, ct: ct).ConfigureAwait(false))
            {
                // Write the self-cleaning, TTL'd in-flight lease marker (write-time TTL = lease duration).
                await _store.PutAsync(_keys.Lease(job.Id), workerId, _options.LeaseDuration, ct).ConfigureAwait(false);
                return claimed;
            }
            // Lost this one (claimed/changed by another worker) — try the next candidate.
        }

        return null;
    }

    private async Task TryDeadLetterStaleAsync(Job job, long version, DateTimeOffset now, CancellationToken ct)
    {
        var dead = job.Clone();
        dead.State = JobState.DeadLettered;
        dead.LeaseOwner = null;
        dead.LastError ??= "lease expired after attempts exhausted (worker presumed crashed)";
        dead.LastFinishedUtc = now;
        if (await _store.CompareAndSwapAsync(_keys.Job(job.Id), version, dead, ct: ct).ConfigureAwait(false))
        {
            await _dlq.AddAsync(job.Id, ct).ConfigureAwait(false);
            await _active.RemoveAsync(job.Id, ct).ConfigureAwait(false);
            await _store.DeleteAsync(_keys.Lease(job.Id), ct).ConfigureAwait(false);
        }
    }

    /// <summary>A job is claimable when it is due and either Ready or a Claimed job whose lease has expired.</summary>
    private static bool IsClaimable(Job job, DateTimeOffset now)
    {
        if (job.VisibleAtUtc > now) return false; // not yet due (delay / schedule / backoff)
        return job.State == JobState.Ready
               || (job.State == JobState.Claimed && job.LeaseExpiresUtc <= now);
    }

    // ----------------------------------------------------------------- ack / fail

    /// <summary>
    /// Acks a successful delivery. Succeeds only if <paramref name="workerId"/> still holds the lease
    /// (its claim CAS was not overtaken by a reclaimer). Returns <c>false</c> if the lease was lost —
    /// the signal that a slower duplicate delivery has already been taken over by another worker, so
    /// this worker must not treat its side effects as the authoritative completion.
    /// </summary>
    public async Task<bool> CompleteAsync(string workerId, string jobId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var doc = await _store.ReadAsync<Job>(_keys.Job(jobId), ct).ConfigureAwait(false);
        if (!doc.Found || doc.Value is null) return false;
        var job = doc.Value;
        if (job.State != JobState.Claimed || job.LeaseOwner != workerId) return false;

        var done = job.Clone();
        done.State = JobState.Completed;
        done.LeaseOwner = null;
        done.LastFinishedUtc = now;

        // Retain the finished document for a window, then let Zaris reclaim it via write-time TTL.
        if (!await _store.CompareAndSwapAsync(_keys.Job(jobId), doc.Version, done, _options.CompletedRetention, ct)
                .ConfigureAwait(false))
            return false; // reclaimed out from under us.

        await _active.RemoveAsync(jobId, ct).ConfigureAwait(false);
        await _store.DeleteAsync(_keys.Lease(jobId), ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Reports a failed delivery. If attempts remain, the job goes back to Ready with an exponential
    /// backoff delay; otherwise it is dead-lettered. Only the current lease owner may do this.
    /// </summary>
    public async Task<FailResult> FailAsync(string workerId, string jobId, string error, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var doc = await _store.ReadAsync<Job>(_keys.Job(jobId), ct).ConfigureAwait(false);
        if (!doc.Found || doc.Value is null) return new FailResult(FailKind.LostLease, null, 0);
        var job = doc.Value;
        if (job.State != JobState.Claimed || job.LeaseOwner != workerId)
            return new FailResult(FailKind.LostLease, null, job.Attempts);

        if (job.Attempts >= job.MaxAttempts)
        {
            var dead = job.Clone();
            dead.State = JobState.DeadLettered;
            dead.LeaseOwner = null;
            dead.LastError = error;
            dead.LastFinishedUtc = now;
            if (!await _store.CompareAndSwapAsync(_keys.Job(jobId), doc.Version, dead, ct: ct).ConfigureAwait(false))
                return new FailResult(FailKind.LostLease, null, job.Attempts);

            await _dlq.AddAsync(jobId, ct).ConfigureAwait(false);
            await _active.RemoveAsync(jobId, ct).ConfigureAwait(false);
            await _store.DeleteAsync(_keys.Lease(jobId), ct).ConfigureAwait(false);
            return new FailResult(FailKind.DeadLettered, null, job.Attempts);
        }

        var backoff = Backoff.For(job.Attempts, _options.RetryBackoffBase, _options.RetryBackoffCap, _options.UseBackoffJitter);
        var nextVisible = now + backoff;
        var retry = job.Clone();
        retry.State = JobState.Ready;
        retry.LeaseOwner = null;
        retry.VisibleAtUtc = nextVisible;
        retry.LastError = error;
        if (!await _store.CompareAndSwapAsync(_keys.Job(jobId), doc.Version, retry, ct: ct).ConfigureAwait(false))
            return new FailResult(FailKind.LostLease, null, job.Attempts);

        await _store.DeleteAsync(_keys.Lease(jobId), ct).ConfigureAwait(false);
        return new FailResult(FailKind.Retried, nextVisible, job.Attempts);
    }

    // ----------------------------------------------------------------- inspection

    public async Task<Job?> GetAsync(string jobId, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<Job>(_keys.Job(jobId), ct).ConfigureAwait(false);
        return doc.Found ? doc.Value : null;
    }

    /// <summary>True while the TTL'd in-flight lease marker still exists in the store (self-cleans on crash).</summary>
    public Task<bool> IsLeaseMarkerLiveAsync(string jobId, CancellationToken ct = default) =>
        _store.ExistsAsync(_keys.Lease(jobId), ct);

    public Task<IReadOnlyList<string>> ListActiveAsync(CancellationToken ct = default) => _active.ListAsync(ct);
    public Task<IReadOnlyList<string>> ListDeadLetterAsync(CancellationToken ct = default) => _dlq.ListAsync(ct);

    /// <summary>A point-in-time count of jobs by state (reads the index + each job document).</summary>
    public async Task<QueueStats> StatsAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var stats = new QueueStats();
        foreach (var id in await _active.ListAsync(ct).ConfigureAwait(false))
        {
            var job = await GetAsync(id, ct).ConfigureAwait(false);
            if (job is null) continue;
            switch (job.State)
            {
                case JobState.Ready when job.VisibleAtUtc <= now: stats.ReadyDue++; break;
                case JobState.Ready: stats.ReadyScheduled++; break;
                case JobState.Claimed when job.LeaseExpiresUtc <= now: stats.LeaseExpired++; break;
                case JobState.Claimed: stats.InFlight++; break;
            }
        }
        stats.DeadLettered = await _dlq.CountAsync(ct).ConfigureAwait(false);
        return stats;
    }
}

/// <summary>A snapshot of queue depth by state.</summary>
public sealed class QueueStats
{
    public int ReadyDue { get; set; }
    public int ReadyScheduled { get; set; }
    public int InFlight { get; set; }
    public int LeaseExpired { get; set; }
    public int DeadLettered { get; set; }

    public override string ToString() =>
        $"ready-due={ReadyDue} scheduled={ReadyScheduled} in-flight={InFlight} lease-expired={LeaseExpired} dlq={DeadLettered}";
}
