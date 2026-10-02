using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RateLimiterGateway.RateLimiting.Tests;

public sealed class SlidingWindowLogLimiterTests
{
    private static RateLimitPolicy Policy(long limit, int windowSecs) => new()
    {
        Name = "sw-test",
        Algorithm = RateLimitAlgorithm.SlidingWindowLog,
        Limit = limit,
        Window = TimeSpan.FromSeconds(windowSecs)
    };

    [Fact]
    public async Task Admits_exactly_limit_then_rejects_at_boundary()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new SlidingWindowLogLimiter(store, clock);
        var policy = Policy(5, 10);

        for (var i = 0; i < 5; i++)
        {
            var d = await sut.AcquireAsync("user-1", policy);
            Assert.True(d.Allowed, $"request {i} should be allowed");
            Assert.Equal(5 - (i + 1), d.Remaining);
        }

        var over = await sut.AcquireAsync("user-1", policy);
        Assert.False(over.Allowed);
        Assert.Equal(0, over.Remaining);
        Assert.True(over.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task Frees_slots_as_window_slides()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new SlidingWindowLogLimiter(store, clock);
        var policy = Policy(3, 10);

        for (var i = 0; i < 3; i++) Assert.True((await sut.AcquireAsync("u", policy)).Allowed);
        Assert.False((await sut.AcquireAsync("u", policy)).Allowed);

        // Advance past the window: all three logged timestamps age out → full quota again.
        clock.Advance(TimeSpan.FromSeconds(11));
        for (var i = 0; i < 3; i++) Assert.True((await sut.AcquireAsync("u", policy)).Allowed);
        Assert.False((await sut.AcquireAsync("u", policy)).Allowed);
    }

    [Fact]
    public async Task Partial_slide_frees_only_expired_slots()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new SlidingWindowLogLimiter(store, clock);
        var policy = Policy(2, 10);

        Assert.True((await sut.AcquireAsync("u", policy)).Allowed);   // t=0
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.True((await sut.AcquireAsync("u", policy)).Allowed);   // t=6
        Assert.False((await sut.AcquireAsync("u", policy)).Allowed);  // t=6 full

        // At t=11 the first (t=0) has aged out but the second (t=6) is still in-window → exactly one slot.
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True((await sut.AcquireAsync("u", policy)).Allowed);   // t=11
        Assert.False((await sut.AcquireAsync("u", policy)).Allowed);
    }

    [Fact]
    public async Task Concurrent_burst_never_over_admits()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new SlidingWindowLogLimiter(store, clock);
        var policy = Policy(50, 60);

        // 1000 racing requests in the same window against one resource → exactly 50 admitted.
        var tasks = Enumerable.Range(0, 1000).Select(_ => sut.AcquireAsync("hot", policy)).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(50, results.Count(r => r.Allowed));
        Assert.Equal(950, results.Count(r => !r.Allowed));
    }

    [Fact]
    public async Task Separate_resources_have_independent_quotas()
    {
        var (store, clock) = await TestStore.NewAsync();
        var sut = new SlidingWindowLogLimiter(store, clock);
        var policy = Policy(2, 10);

        Assert.True((await sut.AcquireAsync("a", policy)).Allowed);
        Assert.True((await sut.AcquireAsync("a", policy)).Allowed);
        Assert.False((await sut.AcquireAsync("a", policy)).Allowed);
        // Different key → its own quota, unaffected.
        Assert.True((await sut.AcquireAsync("b", policy)).Allowed);
        Assert.True((await sut.AcquireAsync("b", policy)).Allowed);
        Assert.False((await sut.AcquireAsync("b", policy)).Allowed);
    }
}
