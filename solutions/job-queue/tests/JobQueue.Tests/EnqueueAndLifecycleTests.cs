using JobQueue.Queue;
using Xunit;

namespace JobQueue.Tests;

/// <summary>Enqueue semantics: delay/schedule visibility, priority ordering, ack, and idempotent enqueue.</summary>
public class EnqueueAndLifecycleTests
{
    [Fact]
    public async Task Delayed_job_is_invisible_until_its_due_time()
    {
        var w = await TestWorld.NewAsync();
        var id = await w.Queue.EnqueueAsync("later", delay: TimeSpan.FromSeconds(10));

        Assert.Null(await w.Queue.TryClaimAsync("w1")); // not due yet
        w.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Null(await w.Queue.TryClaimAsync("w1"));
        w.Clock.Advance(TimeSpan.FromSeconds(1));
        var job = await w.Queue.TryClaimAsync("w1"); // now due
        Assert.NotNull(job);
        Assert.Equal(id, job!.Id);
    }

    [Fact]
    public async Task Scheduled_for_specific_time_becomes_visible_at_that_time()
    {
        var w = await TestWorld.NewAsync();
        var when = w.Clock.UtcNow + TimeSpan.FromMinutes(5);
        await w.Queue.EnqueueAsync("scheduled", scheduledFor: when);

        Assert.Null(await w.Queue.TryClaimAsync("w1"));
        w.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.NotNull(await w.Queue.TryClaimAsync("w1"));
    }

    [Fact]
    public async Task Higher_priority_job_is_claimed_first()
    {
        var w = await TestWorld.NewAsync();
        await w.Queue.EnqueueAsync("low", priority: 1, jobId: "low");
        await w.Queue.EnqueueAsync("high", priority: 10, jobId: "high");
        await w.Queue.EnqueueAsync("mid", priority: 5, jobId: "mid");

        Assert.Equal("high", (await w.Queue.TryClaimAsync("w"))!.Id);
        Assert.Equal("mid", (await w.Queue.TryClaimAsync("w"))!.Id);
        Assert.Equal("low", (await w.Queue.TryClaimAsync("w"))!.Id);
    }

    [Fact]
    public async Task Complete_removes_job_from_active_set()
    {
        var w = await TestWorld.NewAsync();
        var id = await w.Queue.EnqueueAsync("p");
        Assert.Contains(id, await w.Queue.ListActiveAsync());

        var job = await w.Queue.TryClaimAsync("w1");
        Assert.True(await w.Queue.CompleteAsync("w1", job!.Id));

        Assert.DoesNotContain(id, await w.Queue.ListActiveAsync());
        Assert.False(await w.Queue.IsLeaseMarkerLiveAsync(id)); // lease marker cleaned up
        var done = await w.Queue.GetAsync(id);
        Assert.Equal(JobState.Completed, done!.State);
    }

    [Fact]
    public async Task Idempotent_enqueue_with_stable_id_does_not_duplicate()
    {
        var w = await TestWorld.NewAsync();
        await w.Queue.EnqueueAsync("v1", jobId: "order-42");
        await w.Queue.EnqueueAsync("v2", jobId: "order-42"); // same id → no-op on payload

        var active = await w.Queue.ListActiveAsync();
        Assert.Single(active, x => x == "order-42");
        var job = await w.Queue.GetAsync("order-42");
        Assert.Equal("v1", job!.Payload); // first write wins; create is IfAbsent
    }
}
