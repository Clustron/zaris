using JobQueue.Queue;
using Xunit;

namespace JobQueue.Tests;

/// <summary>
/// Lease-expiry requeue — the crash-recovery heart of the queue. A claimed job whose lease has expired
/// becomes claimable again by any worker; the original (crashed) owner can no longer ack it.
/// </summary>
public class LeaseExpiryTests
{
    [Fact]
    public async Task Claimed_job_is_not_reclaimable_while_lease_is_live()
    {
        var w = await TestWorld.NewAsync(o => o.LeaseDuration = TimeSpan.FromSeconds(30));
        var id = await w.Queue.EnqueueAsync("p");

        var first = await w.Queue.TryClaimAsync("w1");
        Assert.NotNull(first);

        // Advance less than the lease — nobody else can take it.
        w.Clock.Advance(TimeSpan.FromSeconds(10));
        var second = await w.Queue.TryClaimAsync("w2");
        Assert.Null(second);
    }

    [Fact]
    public async Task Expired_lease_lets_another_worker_reclaim_and_increments_attempts()
    {
        var w = await TestWorld.NewAsync(o => o.LeaseDuration = TimeSpan.FromSeconds(30));
        var id = await w.Queue.EnqueueAsync("p");

        var first = await w.Queue.TryClaimAsync("w1");
        Assert.Equal(1, first!.Attempts);

        // w1 "crashes" (never acks). Advance past the lease.
        w.Clock.Advance(TimeSpan.FromSeconds(31));

        var second = await w.Queue.TryClaimAsync("w2");
        Assert.NotNull(second);
        Assert.Equal("w2", second!.LeaseOwner);
        Assert.Equal(2, second.Attempts); // the reclaim counts as a new delivery
    }

    [Fact]
    public async Task Crashed_owner_cannot_complete_after_lease_is_reclaimed()
    {
        var w = await TestWorld.NewAsync(o => o.LeaseDuration = TimeSpan.FromSeconds(30));
        var id = await w.Queue.EnqueueAsync("p");

        await w.Queue.TryClaimAsync("w1");
        w.Clock.Advance(TimeSpan.FromSeconds(31));
        var second = await w.Queue.TryClaimAsync("w2");
        Assert.Equal("w2", second!.LeaseOwner);

        // The slow original owner tries to ack — it must be rejected (lease lost to w2).
        Assert.False(await w.Queue.CompleteAsync("w1", id));
        // The rightful owner can ack.
        Assert.True(await w.Queue.CompleteAsync("w2", id));

        var job = await w.Queue.GetAsync(id);
        Assert.Equal(JobState.Completed, job!.State);
    }

    [Fact]
    public async Task Reclaim_of_crashed_job_that_exhausted_attempts_goes_to_dead_letter()
    {
        // maxAttempts = 1: a single crash uses the only delivery, so the reclaimer must dead-letter it.
        var w = await TestWorld.NewAsync(o => o.LeaseDuration = TimeSpan.FromSeconds(10));
        var id = await w.Queue.EnqueueAsync("p", maxAttempts: 1);

        var first = await w.Queue.TryClaimAsync("w1");
        Assert.Equal(1, first!.Attempts);

        w.Clock.Advance(TimeSpan.FromSeconds(11)); // lease expires, w1 presumed crashed

        var second = await w.Queue.TryClaimAsync("w2");
        Assert.Null(second); // nothing claimable — it was retired instead

        var job = await w.Queue.GetAsync(id);
        Assert.Equal(JobState.DeadLettered, job!.State);
        Assert.Contains(id, await w.Queue.ListDeadLetterAsync());
    }

    [Fact]
    public async Task Lease_marker_key_self_reclaims_via_write_time_TTL()
    {
        // This test uses the REAL store wall-clock TTL (not the manual clock): the in-flight lease
        // marker is written with PutOptions.Metadata.Ttl and must disappear on its own.
        var w = await TestWorld.NewAsync(o => o.LeaseDuration = TimeSpan.FromMilliseconds(300));
        var id = await w.Queue.EnqueueAsync("p");

        await w.Queue.TryClaimAsync("w1");
        Assert.True(await w.Queue.IsLeaseMarkerLiveAsync(id)); // marker present right after claim

        // Wait past the TTL; the store reclaims the marker key by itself.
        await WaitUntilAsync(async () => !await w.Queue.IsLeaseMarkerLiveAsync(id), TimeSpan.FromSeconds(5));
        Assert.False(await w.Queue.IsLeaseMarkerLiveAsync(id));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
    }
}
