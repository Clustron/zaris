using System;
using System.Threading.Tasks;
using Clustron.Zaris.Client;
using RateLimiterGateway.RateLimiting;

namespace RateLimiterGateway.RateLimiting.Tests;

/// <summary>
/// Spins up an embedded (in-proc) Zaris store with a unique name per test, so each test's keyspace
/// is isolated while still exercising the REAL Zaris client + CAS path the production code uses.
/// </summary>
public static class TestStore
{
    public static async Task<IZarisClient> ConnectUniqueAsync()
        => await ZarisStoreFactory.ConnectAsync($"zaris://inproc/test-{Guid.NewGuid():N}");

    public static async Task<(ZarisStateStore store, TestClock clock)> NewAsync()
    {
        var client = await ConnectUniqueAsync();
        return (new ZarisStateStore(client), new TestClock(DateTimeOffset.UnixEpoch.AddDays(1)));
    }
}
