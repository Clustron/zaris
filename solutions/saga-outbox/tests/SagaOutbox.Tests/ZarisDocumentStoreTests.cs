using SagaOutbox.Infrastructure;
using Xunit;

namespace SagaOutbox.Tests;

/// <summary>The CAS primitive everything is built on, tested directly against the real Zaris client.</summary>
public class ZarisDocumentStoreTests
{
    private sealed class Counter { public int Value { get; set; } }

    [Fact]
    public async Task CreateAsync_succeeds_once_then_loses_the_race()
    {
        var w = await World.NewAsync();
        Assert.True(await w.Store.CreateAsync("k", new Counter { Value = 1 }));
        Assert.False(await w.Store.CreateAsync("k", new Counter { Value = 2 }));
        var doc = await w.Store.ReadAsync<Counter>("k");
        Assert.Equal(1, doc.Value!.Value); // first create won
    }

    [Fact]
    public async Task CompareAndSwap_rejects_a_stale_version()
    {
        var w = await World.NewAsync();
        await w.Store.CreateAsync("k", new Counter { Value = 0 });
        var read = await w.Store.ReadAsync<Counter>("k");

        // First CAS at the read version wins and bumps the version.
        Assert.True(await w.Store.CompareAndSwapAsync("k", read.Version, new Counter { Value = 1 }));
        // Second CAS at the now-stale version must be rejected.
        Assert.False(await w.Store.CompareAndSwapAsync("k", read.Version, new Counter { Value = 99 }));

        var after = await w.Store.ReadAsync<Counter>("k");
        Assert.Equal(1, after.Value!.Value);
    }

    [Fact]
    public async Task MutateAsync_serializes_concurrent_increments_without_loss()
    {
        var w = await World.NewAsync();
        const int writers = 50;

        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
            await w.Store.MutateAsync<Counter>("ctr",
                c => { c.Value++; return c; },
                create: () => new Counter { Value = 0 })));
        await Task.WhenAll(tasks);

        var doc = await w.Store.ReadAsync<Counter>("ctr");
        Assert.Equal(writers, doc.Value!.Value); // no lost updates despite contention
    }

    [Fact]
    public async Task MutateAsync_null_is_a_no_op_and_does_not_bump_the_version()
    {
        var w = await World.NewAsync();
        await w.Store.CreateAsync("k", new Counter { Value = 7 });
        var before = await w.Store.ReadAsync<Counter>("k");

        await w.Store.MutateAsync<Counter>("k", _ => null, create: () => new Counter());

        var after = await w.Store.ReadAsync<Counter>("k");
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(7, after.Value!.Value);
    }
}
