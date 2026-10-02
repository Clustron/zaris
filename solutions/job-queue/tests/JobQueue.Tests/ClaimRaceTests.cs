using JobQueue.Queue;
using Xunit;

namespace JobQueue.Tests;

/// <summary>
/// The core safety property: a claim is a single CAS on the job document, so no matter how many
/// workers pounce at once, exactly one wins. These tests hammer that with real concurrency against the
/// real store.
/// </summary>
public class ClaimRaceTests
{
    [Fact]
    public async Task Hundred_concurrent_workers_exactly_one_claims_a_single_job()
    {
        var w = await TestWorld.NewAsync();
        var id = await w.Queue.EnqueueAsync("payload", priority: 5);

        const int workers = 100;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, workers).Select(i => Task.Run(async () =>
        {
            await start.Task; // release all at once for maximum contention
            return await w.Queue.TryClaimAsync($"w{i}");
        })).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        var winners = results.Where(r => r is not null).ToList();
        Assert.Single(winners);
        Assert.Equal(id, winners[0]!.Id);
        Assert.Equal(JobState.Claimed, winners[0]!.State);
        Assert.Equal(1, winners[0]!.Attempts);

        // The store agrees there is exactly one owner.
        var job = await w.Queue.GetAsync(id);
        Assert.Equal(JobState.Claimed, job!.State);
        Assert.StartsWith("w", job.LeaseOwner);
    }

    [Fact]
    public async Task Concurrent_workers_over_many_jobs_claim_each_job_at_most_once()
    {
        var w = await TestWorld.NewAsync();
        const int jobs = 200;
        for (var i = 0; i < jobs; i++)
            await w.Queue.EnqueueAsync($"p{i}");

        // 20 workers each drain greedily until the queue is empty; collect every claimed id.
        var claimedIds = new System.Collections.Concurrent.ConcurrentBag<string>();
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            while (true)
            {
                var job = await w.Queue.TryClaimAsync($"w{i}");
                if (job is null) break;
                claimedIds.Add(job.Id);
                await w.Queue.CompleteAsync($"w{i}", job.Id);
            }
        })).ToArray();
        await Task.WhenAll(tasks);

        // Every job claimed exactly once (no duplicates), and all of them claimed.
        var list = claimedIds.ToList();
        Assert.Equal(jobs, list.Count);
        Assert.Equal(jobs, list.Distinct().Count());
    }
}
