using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RateLimiterGateway.RateLimiting.Tests;

public sealed class TokenBucketLimiterTests
{
    private static RateLimitPolicy Policy(long capacity, double rate) => new()
    {
        Name = "tb-test",
        Algorithm = RateLimitAlgorithm.TokenBucket,
        Limit = capacity,
        RefillPerSecond = rate
    };

    [Fact]
    public async Task Fresh_bucket_allows_full_capacity_then_rejects()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new TokenBucketLimiter(store, clock);
        var policy = Policy(10, 1); // 10 tokens, slow refill so none accrues within the test

        for (var i = 0; i < 10; i++)
            Assert.True((await sut.AcquireAsync("u", policy)).Allowed, $"token {i}");

        var over = await sut.AcquireAsync("u", policy);
        Assert.False(over.Allowed);
        Assert.True(over.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task Refills_over_time_up_to_capacity()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new TokenBucketLimiter(store, clock);
        var policy = Policy(10, 5); // refill 5 tokens/sec

        for (var i = 0; i < 10; i++) Assert.True((await sut.AcquireAsync("u", policy)).Allowed);
        Assert.False((await sut.AcquireAsync("u", policy)).Allowed);

        // After 1s, 5 tokens have refilled → exactly 5 more allowed, then empty again.
        clock.Advance(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 5; i++) Assert.True((await sut.AcquireAsync("u", policy)).Allowed, $"refilled {i}");
        Assert.False((await sut.AcquireAsync("u", policy)).Allowed);
    }

    [Fact]
    public async Task Refill_is_capped_at_capacity()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new TokenBucketLimiter(store, clock);
        var policy = Policy(10, 5);

        for (var i = 0; i < 10; i++) await sut.AcquireAsync("u", policy); // drain
        // Idle for a long time: refill must not exceed capacity (no "saved up" tokens beyond 10).
        clock.Advance(TimeSpan.FromMinutes(10));

        var admitted = 0;
        for (var i = 0; i < 100; i++)
            if ((await sut.AcquireAsync("u", policy)).Allowed) admitted++;
            else break;
        Assert.Equal(10, admitted);
    }

    [Fact]
    public async Task Concurrent_burst_never_over_draws_capacity()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new TokenBucketLimiter(store, clock);
        var policy = Policy(100, 0); // refill 0 so the bucket can't gain tokens mid-burst

        var tasks = Enumerable.Range(0, 1000).Select(_ => sut.AcquireAsync("hot", policy)).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(100, results.Count(r => r.Allowed));
        Assert.Equal(900, results.Count(r => !r.Allowed));
    }

    [Fact]
    public async Task Remaining_reflects_tokens_left()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new TokenBucketLimiter(store, clock);
        var policy = Policy(10, 0);

        var d1 = await sut.AcquireAsync("u", policy);
        Assert.True(d1.Allowed);
        Assert.Equal(9, d1.Remaining);
        var d2 = await sut.AcquireAsync("u", policy);
        Assert.Equal(8, d2.Remaining);
    }
}
