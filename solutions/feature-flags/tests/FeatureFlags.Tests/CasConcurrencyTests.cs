using FeatureFlags.Model;
using Xunit;

namespace FeatureFlags.Tests;

/// <summary>
/// Concurrency correctness of the admin's CAS read-modify-write on the single ruleset document. These
/// tests run against the REAL embedded Zaris store and its CAS path — no mocks.
/// </summary>
public sealed class CasConcurrencyTests
{
    [Fact]
    public async Task Concurrent_edits_to_different_flags_all_survive()
    {
        await using var w = await TestWorld.NewAsync();

        // Seed 20 boolean flags, all off.
        for (var i = 0; i < 20; i++)
            await w.Admin.DefineFlagAsync(FlagDefinition.Boolean($"flag-{i}", enabled: false), "seed");

        // 20 admins (each its own connection) concurrently enable a distinct flag.
        var admins = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => w.NewAdminAsync()));
        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => admins[i].SetEnabledAsync($"flag-{i}", true, $"admin-{i}")));

        // Every single edit must be present — none lost to a racing writer.
        var ruleset = await w.Admin.GetRulesetAsync();
        for (var i = 0; i < 20; i++)
            Assert.True(ruleset.Flags[$"flag-{i}"].Enabled, $"flag-{i} lost its update");
    }

    [Fact]
    public async Task Concurrent_toggles_of_the_same_flag_do_not_corrupt_and_version_advances_per_commit()
    {
        await using var w = await TestWorld.NewAsync();
        await w.Admin.DefineFlagAsync(FlagDefinition.Boolean("hot", enabled: false), "seed");
        var startVersion = (await w.Admin.GetRulesetAsync()).Version;

        // 50 concurrent writers each append a distinct targeted user to the same flag's target list.
        var admins = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => w.NewAdminAsync()));
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            admins[i].UpdateFlagAsync("hot", "add-target", $"admin-{i}",
                f => $"targets={f.Targets.Count}",
                f =>
                {
                    if (f.Targets.Count == 0) f.Targets.Add(new Target(0));
                    f.Targets[0].UserKeys.Add($"user-{i}");
                })));

        var ruleset = await w.Admin.GetRulesetAsync();
        var users = ruleset.Flags["hot"].Targets[0].UserKeys;
        // All 50 appends landed (no lost update), and each committed edit bumped the version by one.
        Assert.Equal(50, users.Distinct().Count());
        Assert.Equal(startVersion + 50, ruleset.Version);
    }
}
