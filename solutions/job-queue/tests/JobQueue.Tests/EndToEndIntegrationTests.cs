using System.Collections.Concurrent;
using JobQueue.Infrastructure;
using JobQueue.Queue;
using Xunit;
using Xunit.Abstractions;

namespace JobQueue.Tests;

/// <summary>
/// The whole thing, end to end, against the real embedded store with real concurrency: many producers
/// enqueue, many competing workers drain, some deliveries crash mid-work (forcing lease-expiry
/// requeue), and a batch of poison jobs always fail (forcing retry → DLQ). Uses the wall clock with a
/// short lease so real time drives reclamation.
///
/// Proven guarantees:
///   * no job is lost      — every job ends terminal (Completed or DeadLettered);
///   * at-least-once       — every good job's handler ran at least once (crashed ones more than once);
///   * no double-complete  — authoritative completions == number of good jobs, each exactly once;
///   * DLQ captures poison — exactly the poison jobs are dead-lettered.
/// </summary>
public class EndToEndIntegrationTests
{
    private readonly ITestOutputHelper _out;
    public EndToEndIntegrationTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public async Task Many_workers_with_crashes_and_poison_jobs_lose_nothing_and_capture_the_DLQ()
    {
        var client = await ZarisConnection.ConnectAsync($"zaris://inproc/jq-e2e-{Guid.NewGuid():N}");
        var store = new ZarisDocumentStore(client);
        var options = new JobQueueOptions
        {
            LeaseDuration = TimeSpan.FromMilliseconds(250),       // short so a crashed job requeues fast
            DefaultMaxAttempts = 4,
            RetryBackoffBase = TimeSpan.FromMilliseconds(20),
            RetryBackoffCap = TimeSpan.FromMilliseconds(100),
            UseBackoffJitter = false,
        };
        var queue = new JobQueueClient(store, "e2e", options, SystemClock.Instance);

        const int goodJobs = 50;
        const int poisonJobs = 10;
        var goodIds = new HashSet<string>();
        var poisonIds = new HashSet<string>();
        var crashOnceIds = new HashSet<string>(); // good jobs whose first delivery crashes

        // ---- producers: several threads enqueue concurrently ----
        var rnd = new Random(1234);
        for (var i = 0; i < goodJobs; i++)
        {
            var id = $"good-{i}";
            goodIds.Add(id);
            if (i % 3 == 0) crashOnceIds.Add(id); // ~1/3 crash on first delivery
        }
        for (var i = 0; i < poisonJobs; i++) poisonIds.Add($"poison-{i}");

        var allToEnqueue = goodIds.Concat(poisonIds).OrderBy(_ => rnd.Next()).ToList();
        await Task.WhenAll(Partition(allToEnqueue, 5).Select(chunk => Task.Run(async () =>
        {
            foreach (var id in chunk)
            {
                var maxAttempts = poisonIds.Contains(id) ? 2 : options.DefaultMaxAttempts;
                await queue.EnqueueAsync(id, priority: Random.Shared.Next(0, 3),
                    maxAttempts: maxAttempts, jobId: id);
            }
        })));

        // ---- handler: records runs; crashes once for flagged jobs; poison always fails ----
        var runsPerJob = new ConcurrentDictionary<string, int>();
        Func<Job, CancellationToken, Task> handler = (job, _) =>
        {
            var runs = runsPerJob.AddOrUpdate(job.Id, 1, (_, n) => n + 1);
            if (poisonIds.Contains(job.Id))
                throw new InvalidOperationException($"poison {job.Id}");
            if (crashOnceIds.Contains(job.Id) && runs == 1)
                throw new WorkerCrashException($"crash {job.Id} on first delivery");
            return Task.CompletedTask; // success
        };

        // ---- 8 competing workers drain until everything is terminal ----
        const int workerCount = 8;
        var workers = Enumerable.Range(0, workerCount)
            .Select(i => new Worker($"w{i}", queue, handler, idleDelay: TimeSpan.FromMilliseconds(10)))
            .ToArray();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var runTasks = workers.Select(wk => wk.RunAsync(cts.Token)).ToArray();

        // Monitor until drained: nothing active and all poison dead-lettered.
        var drained = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(55);
        while (DateTime.UtcNow < deadline)
        {
            var s = await queue.StatsAsync();
            var active = s.ReadyDue + s.ReadyScheduled + s.InFlight + s.LeaseExpired;
            if (active == 0 && s.DeadLettered == poisonJobs) { drained = true; break; }
            await Task.Delay(25);
        }
        cts.Cancel();
        try { await Task.WhenAll(runTasks); } catch (OperationCanceledException) { }

        Assert.True(drained, "queue did not drain in time");

        // ---- assertions ----
        // No loss: every job ended terminal in the expected bucket.
        foreach (var id in goodIds)
            Assert.Equal(JobState.Completed, (await queue.GetAsync(id))!.State);

        var dlq = (await queue.ListDeadLetterAsync()).ToHashSet();
        Assert.Equal(poisonIds, dlq);                                  // exactly the poison jobs
        foreach (var id in poisonIds)
            Assert.Equal(JobState.DeadLettered, (await queue.GetAsync(id))!.State);

        // Active set fully drained.
        Assert.Empty(await queue.ListActiveAsync());

        // At-least-once: every good job ran >= 1; crash-once jobs ran >= 2.
        foreach (var id in goodIds)
            Assert.True(runsPerJob[id] >= 1, $"{id} never ran");
        foreach (var id in crashOnceIds)
            Assert.True(runsPerJob[id] >= 2, $"{id} was not redelivered after its crash");

        // No double-complete: authoritative completions sum to exactly the good jobs.
        var totalCompleted = workers.Sum(wk => wk.Stats.Completed);
        Assert.Equal(goodJobs, totalCompleted);

        var totalCrashed = workers.Sum(wk => wk.Stats.Crashed);
        var totalDead = workers.Sum(wk => wk.Stats.DeadLettered);
        _out.WriteLine($"good={goodJobs} poison={poisonJobs} crashOnce={crashOnceIds.Count}");
        _out.WriteLine($"completed={totalCompleted} crashed={totalCrashed} deadLettered={totalDead}");
        _out.WriteLine($"total handler runs={runsPerJob.Values.Sum()} (>= jobs, proving at-least-once)");
        Assert.Equal(poisonJobs, totalDead);
        Assert.True(totalCrashed >= crashOnceIds.Count);
    }

    private static IEnumerable<List<T>> Partition<T>(IReadOnlyList<T> items, int parts)
    {
        var buckets = Enumerable.Range(0, parts).Select(_ => new List<T>()).ToArray();
        for (var i = 0; i < items.Count; i++) buckets[i % parts].Add(items[i]);
        return buckets;
    }
}
