using JobQueue.Infrastructure;
using JobQueue.Queue;

namespace JobQueue.Tests;

/// <summary>
/// An isolated world for one test: a real embedded Zaris store with a unique name (independent
/// keyspace per test) and a <see cref="JobQueueClient"/> over it, driven by a <see cref="ManualClock"/>
/// so lease/backoff/schedule timing is deterministic. Every test exercises the REAL Zaris client + CAS
/// path — there are no mocks.
/// </summary>
public sealed class TestWorld
{
    public ZarisDocumentStore Store { get; private set; } = null!;
    public JobQueueClient Queue { get; private set; } = null!;
    public ManualClock Clock { get; private set; } = null!;
    public JobQueueOptions Options { get; private set; } = null!;

    public static async Task<TestWorld> NewAsync(Action<JobQueueOptions>? configure = null)
    {
        var client = await ZarisConnection.ConnectAsync($"zaris://inproc/jq-test-{Guid.NewGuid():N}");
        var store = new ZarisDocumentStore(client);
        var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var options = new JobQueueOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(30),
            DefaultMaxAttempts = 3,
            RetryBackoffBase = TimeSpan.FromSeconds(1),
            RetryBackoffCap = TimeSpan.FromMinutes(1),
            UseBackoffJitter = false,
        };
        configure?.Invoke(options);
        return new TestWorld
        {
            Store = store,
            Clock = clock,
            Options = options,
            Queue = new JobQueueClient(store, "orders", options, clock),
        };
    }

    /// <summary>A no-op handler that always succeeds.</summary>
    public static Func<Job, CancellationToken, Task> Succeed => (_, _) => Task.CompletedTask;

    /// <summary>A handler that always throws (business failure → retry/DLQ).</summary>
    public static Func<Job, CancellationToken, Task> AlwaysFail(string msg = "boom") =>
        (_, _) => throw new InvalidOperationException(msg);
}
