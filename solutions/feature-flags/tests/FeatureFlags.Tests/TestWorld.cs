using FeatureFlags.Admin;
using FeatureFlags.Client;
using FeatureFlags.Infrastructure;

namespace FeatureFlags.Tests;

/// <summary>
/// An isolated world for one test: a real embedded Zaris store with a unique name (independent
/// keyspace per test), an admin over it, and a frozen clock for deterministic audit timestamps.
/// Every test exercises the REAL Zaris client + CAS path — there are no mocks of the store.
/// Extra clients created with <see cref="NewClientAsync"/> connect to the SAME store name, so in one
/// process they share the backing engine — exactly how several app nodes share one ruleset.
/// </summary>
public sealed class TestWorld : IAsyncDisposable
{
    private readonly List<FeatureFlagClient> _clients = new();

    public string StoreName { get; }
    public ZarisDocumentStore Store { get; }
    public FeatureFlagAdmin Admin { get; }
    public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private TestWorld(string storeName, ZarisDocumentStore store, FeatureFlagAdmin admin)
    {
        StoreName = storeName;
        Store = store;
        Admin = admin;
    }

    public static async Task<TestWorld> NewAsync()
    {
        var name = $"zaris://inproc/ff-test-{Guid.NewGuid():N}";
        var client = await ZarisConnection.ConnectAsync(name);
        var store = new ZarisDocumentStore(client);
        TestWorld world = null!;
        var admin = new FeatureFlagAdmin(store, () => world!.Now);
        world = new TestWorld(name, store, admin);
        return world;
    }

    /// <summary>Advances the frozen clock (used to make audit timestamps deterministic).</summary>
    public void Advance(TimeSpan by) => Now += by;

    /// <summary>A second admin over the same store (its own connection) — for concurrent-edit tests.</summary>
    public async Task<FeatureFlagAdmin> NewAdminAsync()
    {
        var client = await ZarisConnection.ConnectAsync(StoreName);
        return new FeatureFlagAdmin(new ZarisDocumentStore(client), () => Now);
    }

    /// <summary>A started SDK client over the same store, with a fast refresh for tests.</summary>
    public async Task<FeatureFlagClient> NewClientAsync(string name = "client", int refreshMs = 100)
    {
        var connection = await ZarisConnection.ConnectAsync(StoreName);
        var client = new FeatureFlagClient(
            new ZarisDocumentStore(connection),
            new FeatureFlagClientOptions { Name = name, RefreshInterval = TimeSpan.FromMilliseconds(refreshMs) });
        await client.StartAsync();
        _clients.Add(client);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _clients)
            await c.DisposeAsync();
    }
}
