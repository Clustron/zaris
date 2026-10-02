using JobQueue.Queue;
using Xunit;

namespace JobQueue.Tests;

/// <summary>Retry with exponential backoff, the attempt cap, and the dead-letter queue.</summary>
public class RetryAndDeadLetterTests
{
    [Fact]
    public async Task Failure_reschedules_with_exponential_backoff_and_is_invisible_until_due()
    {
        var w = await TestWorld.NewAsync(o =>
        {
            o.DefaultMaxAttempts = 5;
            o.RetryBackoffBase = TimeSpan.FromSeconds(2);
            o.UseBackoffJitter = false;
        });
        var id = await w.Queue.EnqueueAsync("p");

        // Attempt 1 fails → backoff = 2 * 2^0 = 2s.
        var j1 = await w.Queue.TryClaimAsync("w1");
        var r1 = await w.Queue.FailAsync("w1", id, "boom");
        Assert.Equal(FailKind.Retried, r1.Kind);

        // Not yet due: no worker can claim it.
        Assert.Null(await w.Queue.TryClaimAsync("w2"));
        w.Clock.Advance(TimeSpan.FromSeconds(2));

        // Now due. Attempt 2 fails → backoff = 2 * 2^1 = 4s.
        var j2 = await w.Queue.TryClaimAsync("w2");
        Assert.NotNull(j2);
        Assert.Equal(2, j2!.Attempts);
        await w.Queue.FailAsync("w2", id, "boom");

        Assert.Null(await w.Queue.TryClaimAsync("w3")); // invisible for < 4s
        w.Clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Null(await w.Queue.TryClaimAsync("w3")); // still < 4s
        w.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.NotNull(await w.Queue.TryClaimAsync("w3")); // 4s elapsed → due
    }

    [Fact]
    public async Task Exhausting_max_attempts_moves_job_to_dead_letter()
    {
        var w = await TestWorld.NewAsync(o =>
        {
            o.DefaultMaxAttempts = 3;
            o.RetryBackoffBase = TimeSpan.FromSeconds(1);
        });
        var id = await w.Queue.EnqueueAsync("poison");

        FailResult last = default;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var job = await w.Queue.TryClaimAsync("w");
            Assert.NotNull(job);
            Assert.Equal(attempt, job!.Attempts);
            last = await w.Queue.FailAsync("w", id, $"fail #{attempt}");
            if (attempt < 3)
            {
                Assert.Equal(FailKind.Retried, last.Kind);
                w.Clock.Advance(TimeSpan.FromMinutes(5)); // skip past any backoff
            }
        }

        Assert.Equal(FailKind.DeadLettered, last.Kind);
        var dead = await w.Queue.GetAsync(id);
        Assert.Equal(JobState.DeadLettered, dead!.State);
        Assert.Equal("fail #3", dead.LastError);
        Assert.Equal(3, dead.Attempts);

        // It is in the DLQ index and no longer claimable.
        Assert.Contains(id, await w.Queue.ListDeadLetterAsync());
        Assert.Null(await w.Queue.TryClaimAsync("w"));
    }

    [Fact]
    public void Backoff_is_exponential_capped_and_deterministic_without_jitter()
    {
        var baseDelay = TimeSpan.FromSeconds(1);
        var cap = TimeSpan.FromSeconds(10);
        Assert.Equal(TimeSpan.FromSeconds(1), Backoff.For(1, baseDelay, cap, jitter: false));
        Assert.Equal(TimeSpan.FromSeconds(2), Backoff.For(2, baseDelay, cap, jitter: false));
        Assert.Equal(TimeSpan.FromSeconds(4), Backoff.For(3, baseDelay, cap, jitter: false));
        Assert.Equal(TimeSpan.FromSeconds(8), Backoff.For(4, baseDelay, cap, jitter: false));
        Assert.Equal(cap, Backoff.For(5, baseDelay, cap, jitter: false));  // 16s capped to 10s
        Assert.Equal(cap, Backoff.For(40, baseDelay, cap, jitter: false)); // no overflow
    }
}
