namespace JobQueue.Queue;

/// <summary>
/// Thrown from a job handler to simulate the worker process dying mid-work: the in-flight job is
/// <b>abandoned</b> — neither acked nor failed — so its lease is left to expire and another worker
/// reclaims it. This is the hard correctness path the queue must get right.
/// </summary>
public sealed class WorkerCrashException : Exception
{
    public WorkerCrashException(string message = "simulated worker crash") : base(message) { }
}

/// <summary>Counters a worker accumulates over its run (for test assertions and demo output).</summary>
public sealed class WorkerStats
{
    private int _claimed, _completed, _failed, _deadLettered, _crashed, _lostLease;
    public int Claimed => _claimed;
    public int Completed => _completed;
    public int Failed => _failed;
    public int DeadLettered => _deadLettered;
    public int Crashed => _crashed;
    public int LostLease => _lostLease;

    internal void IncClaimed() => Interlocked.Increment(ref _claimed);
    internal void IncCompleted() => Interlocked.Increment(ref _completed);
    internal void IncFailed() => Interlocked.Increment(ref _failed);
    internal void IncDeadLettered() => Interlocked.Increment(ref _deadLettered);
    internal void IncCrashed() => Interlocked.Increment(ref _crashed);
    internal void IncLostLease() => Interlocked.Increment(ref _lostLease);
}

/// <summary>
/// A competing worker: it polls the queue, claims the best job under a visibility lease, runs the
/// handler, and then acks (complete), fails (retry/DLQ) or — if the handler throws
/// <see cref="WorkerCrashException"/> — abandons the delivery so lease expiry can requeue it elsewhere.
///
/// Many <see cref="Worker"/> instances, each with its own <see cref="JobQueueClient"/> connection, are
/// the "N competing consumers". Only the claim CAS decides who runs a job, so these can be spread
/// across threads, processes or machines without changing the code.
/// </summary>
public sealed class Worker
{
    private readonly JobQueueClient _queue;
    private readonly Func<Job, CancellationToken, Task> _handler;
    private readonly TimeSpan _idleDelay;

    public string Id { get; }
    public WorkerStats Stats { get; } = new();

    public Worker(
        string id,
        JobQueueClient queue,
        Func<Job, CancellationToken, Task> handler,
        TimeSpan? idleDelay = null)
    {
        Id = id;
        _queue = queue;
        _handler = handler;
        _idleDelay = idleDelay ?? TimeSpan.FromMilliseconds(15);
    }

    /// <summary>
    /// Claims and processes exactly one job if one is available. Returns <c>true</c> if a job was
    /// claimed (regardless of the delivery's outcome), <c>false</c> if the queue had nothing claimable.
    /// Deterministic — the tests drive the queue one step at a time with this.
    /// </summary>
    public async Task<bool> PumpOnceAsync(CancellationToken ct = default)
    {
        var job = await _queue.TryClaimAsync(Id, ct).ConfigureAwait(false);
        if (job is null) return false;
        Stats.IncClaimed();

        try
        {
            await _handler(job, ct).ConfigureAwait(false);
        }
        catch (WorkerCrashException)
        {
            // Die mid-work: do NOT complete or fail. The lease will expire and another worker reclaims.
            Stats.IncCrashed();
            return true;
        }
        catch (Exception ex)
        {
            var r = await _queue.FailAsync(Id, job.Id, ex.Message, ct).ConfigureAwait(false);
            switch (r.Kind)
            {
                case FailKind.Retried: Stats.IncFailed(); break;
                case FailKind.DeadLettered: Stats.IncDeadLettered(); break;
                case FailKind.LostLease: Stats.IncLostLease(); break;
            }
            return true;
        }

        if (await _queue.CompleteAsync(Id, job.Id, ct).ConfigureAwait(false))
            Stats.IncCompleted();
        else
            Stats.IncLostLease(); // a reclaimer took over while we were (slowly) working.
        return true;
    }

    /// <summary>Runs the claim/process loop until cancelled, sleeping briefly whenever the queue is idle.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            bool did;
            try
            {
                did = await PumpOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            if (!did)
            {
                try { await Task.Delay(_idleDelay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
