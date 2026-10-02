using FeatureFlags.Model;
using Xunit;
using Xunit.Abstractions;

namespace FeatureFlags.Tests;

/// <summary>
/// End-to-end: an admin and several SDK clients over ONE shared embedded store. A flag flip and a
/// rollout change made by the admin must reach every running client within the refresh window, with
/// no restart — this is the live-propagation guarantee the whole design exists to provide.
/// </summary>
public sealed class LivePropagationIntegrationTests
{
    private readonly ITestOutputHelper _out;
    public LivePropagationIntegrationTests(ITestOutputHelper output) => _out = output;

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("Condition not met in time.");
            await Task.Delay(15);
        }
    }

    [Fact]
    public async Task A_flag_flip_reaches_all_client_instances_without_restart()
    {
        await using var w = await TestWorld.NewAsync();
        await w.Admin.DefineFlagAsync(FlagDefinition.Boolean("new-checkout", enabled: false, fallthrough: true), "ops");

        // Three independent app nodes, each its own client + connection, polling every 100ms.
        var c1 = await w.NewClientAsync("node-1");
        var c2 = await w.NewClientAsync("node-2");
        var c3 = await w.NewClientAsync("node-3");
        var clients = new[] { c1, c2, c3 };
        var user = new UserContext("user-42");

        // Flag is off everywhere to begin with.
        Assert.All(clients, c => Assert.False(c.BoolVariation("new-checkout", user)));

        // Admin flips it on.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await w.Admin.SetEnabledAsync("new-checkout", true, "release-bot");

        // Every client sees "on" within the refresh window — no restart, no manual refresh.
        await WaitUntil(() => clients.All(c => c.BoolVariation("new-checkout", user)));
        sw.Stop();
        _out.WriteLine($"flip propagated to all 3 nodes in ~{sw.ElapsedMilliseconds} ms (refresh interval 100 ms)");

        Assert.All(clients, c => Assert.True(c.BoolVariation("new-checkout", user)));
        Assert.All(clients, c => Assert.True(c.Version >= 2));
    }

    [Fact]
    public async Task A_rollout_change_propagates_and_is_consistent_across_clients()
    {
        await using var w = await TestWorld.NewAsync();
        await w.Admin.DefineFlagAsync(FlagDefinition.Boolean("beta", enabled: true, fallthrough: false), "ops");

        var a = await w.NewClientAsync("node-A");
        var b = await w.NewClientAsync("node-B");

        // Admin rolls "beta" out to 30%.
        await w.Admin.SetBooleanRolloutAsync("beta", 30, "pm");
        await WaitUntil(() => a.Version >= 2 && b.Version >= 2);

        // Both clients must agree on every user's assignment (same deterministic bucketing) …
        var users = Enumerable.Range(0, 5000).Select(i => new UserContext($"u-{i}")).ToArray();
        var admittedA = users.Count(u => a.BoolVariation("beta", u));
        var disagreements = users.Count(u => a.BoolVariation("beta", u) != b.BoolVariation("beta", u));
        Assert.Equal(0, disagreements);

        // … and ~30% are admitted.
        var pct = 100.0 * admittedA / users.Length;
        _out.WriteLine($"rollout 30%: node-A admitted {admittedA}/{users.Length} = {pct:0.0}%, node-A/node-B disagreements = {disagreements}");
        Assert.InRange(pct, 28.0, 32.0);

        // Admin widens to 60%; after propagation nobody drops out (sticky) and more are admitted.
        var before = users.Where(u => a.BoolVariation("beta", u)).Select(u => u.Key).ToHashSet();
        await w.Admin.SetBooleanRolloutAsync("beta", 60, "pm");
        await WaitUntil(() => a.Version >= 3 && b.Version >= 3);

        var after = users.Where(u => a.BoolVariation("beta", u)).Select(u => u.Key).ToHashSet();
        Assert.ProperSubset(after, before);     // everyone in at 30% is still in at 60%
        _out.WriteLine($"widened 30%→60%: {before.Count} → {after.Count} admitted (strict superset, sticky)");
    }

    [Fact]
    public async Task Clients_raise_an_update_event_only_on_a_version_bump()
    {
        await using var w = await TestWorld.NewAsync();
        await w.Admin.DefineFlagAsync(FlagDefinition.Boolean("f", enabled: false), "ops");

        var c = await w.NewClientAsync("node");
        var updates = 0;
        c.Updated += _ => Interlocked.Increment(ref updates);

        await w.Admin.SetEnabledAsync("f", true, "ops");
        await WaitUntil(() => Volatile.Read(ref updates) >= 1);

        // Idle period: no admin change ⇒ polling must NOT raise further updates.
        var seen = Volatile.Read(ref updates);
        await Task.Delay(400);
        Assert.Equal(seen, Volatile.Read(ref updates));
    }
}
