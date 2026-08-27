using System;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Locks;

/// <summary>
/// Demonstrates the distributed lock primitive (<c>ILocksClient</c>): a cluster-wide
/// mutex keyed by a string, with a TTL so a crashed holder can't wedge the lock forever.
/// This is distinct from Leases (TTL-bound key ownership) and the LeaderElection recipe —
/// it is the dedicated "only one holder at a time" API.
/// </summary>
internal class LocksSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public LocksSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        // -----------------------------------------------------
        // Resolve client
        // -----------------------------------------------------
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        await client.ClearAsync();

        var context = new SampleContext("locks");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // Locks live on IZaris — cast the base client to reach the sub-clients.
        var locks = ((IZaris)client).Locks;

        var lockKey = context.Key("job:42");

        // ============================================================
        // Acquire
        // ============================================================

        ConsoleHelper.Section("Acquire");

        // AcquireAsync returns null when the lock is already held by someone else.
        // We keep this handle in a plain local (not `await using`) so we can release
        // and renew it explicitly below without risking a double-dispose.
        var held = await locks.AcquireAsync(lockKey, TimeSpan.FromSeconds(30));

        if (held is null)
        {
            ConsoleHelper.Error($"Could not acquire lock on {lockKey}");
            return;
        }

        ConsoleHelper.Success($"Acquired lock on {lockKey}");
        Console.WriteLine($"Expires (UTC): {held.ExpiryUtc:O}");

        // ============================================================
        // Contention — a second acquire on the same key must be refused
        // ============================================================

        ConsoleHelper.Section("Contention");

        var second = await locks.AcquireAsync(lockKey, TimeSpan.FromSeconds(5));

        if (second is null)
        {
            ConsoleHelper.Success("Second acquire correctly blocked (lock is held)");
        }
        else
        {
            ConsoleHelper.Error("Second acquire unexpectedly succeeded while the lock was held");
            await second.ReleaseAsync();
        }

        // ============================================================
        // Renew — extend the TTL while still holding the lock
        // ============================================================

        ConsoleHelper.Section("Renew");

        await held.RenewAsync(TimeSpan.FromSeconds(30));
        Console.WriteLine($"Renewed — new expiry (UTC): {held.ExpiryUtc:O}");
        ConsoleHelper.Success("Renewed");

        // ============================================================
        // Release + re-acquire — once released, the key is free again
        // ============================================================

        ConsoleHelper.Section("Release + Re-acquire");

        await held.ReleaseAsync();
        ConsoleHelper.Info("Released the lock");

        await using (var reacquired = await locks.AcquireAsync(lockKey, TimeSpan.FromSeconds(10)))
        {
            if (reacquired is not null)
                ConsoleHelper.Success("Re-acquired the lock after release");
            else
                ConsoleHelper.Error("Could not re-acquire after release");
        } // disposal releases the re-acquired lock

        // ============================================================
        // Mutual exclusion under concurrency
        //   Three workers race for the same lock; a shared counter proves
        //   at most one of them is ever inside the critical section.
        // ============================================================

        ConsoleHelper.Section("Mutual Exclusion Under Concurrency");

        var contendedKey = context.Key("critical-section");
        var concurrentInside = 0;
        var maxObserved = 0;
        var successfulEntries = 0;
        var sync = new object();

        async Task Worker()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await using var l = await locks.AcquireAsync(contendedKey, TimeSpan.FromSeconds(2));
                if (l is null)
                {
                    await Task.Delay(10);
                    continue;
                }

                var now = Interlocked.Increment(ref concurrentInside);
                lock (sync)
                {
                    if (now > maxObserved) maxObserved = now;
                    successfulEntries++;
                }

                await Task.Delay(15); // hold the critical section briefly
                Interlocked.Decrement(ref concurrentInside);
            }
        }

        await Task.WhenAll(Worker(), Worker(), Worker());

        Console.WriteLine(
            $"Critical-section entries: {successfulEntries}, " +
            $"max concurrent holders observed: {maxObserved}");

        if (maxObserved <= 1)
            ConsoleHelper.Success("Mutual exclusion held — never more than one holder at a time");
        else
            ConsoleHelper.Error($"Mutual exclusion VIOLATED — observed {maxObserved} concurrent holders");

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}
