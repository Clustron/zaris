using FeatureFlags.Model;
using Xunit;

namespace FeatureFlags.Tests;

/// <summary>The audit log: appended via CAS, gap-free sequence, old → new captured, concurrency-safe.</summary>
public sealed class AuditTests
{
    [Fact]
    public async Task Each_change_appends_one_audit_entry_with_old_and_new()
    {
        await using var w = await TestWorld.NewAsync();
        await w.Admin.DefineFlagAsync(FlagDefinition.Boolean("f", enabled: false), "alice");
        w.Advance(TimeSpan.FromMinutes(1));
        await w.Admin.SetEnabledAsync("f", true, "bob");

        var audit = await w.Admin.GetAuditAsync();
        Assert.Equal(2, audit.Count);

        Assert.Equal("define", audit[0].Action);
        Assert.Equal("alice", audit[0].Actor);
        Assert.Null(audit[0].Old);             // created from nothing

        Assert.Equal("toggle", audit[1].Action);
        Assert.Equal("bob", audit[1].Actor);
        Assert.Equal("Enabled=False", audit[1].Old);
        Assert.Equal("Enabled=True", audit[1].New);

        // Sequence is gap-free and strictly increasing; timestamps come from the frozen clock.
        Assert.Equal(new[] { 1L, 2L }, audit.Select(a => a.Seq).ToArray());
        Assert.True(audit[1].AtUtc > audit[0].AtUtc);
    }

    [Fact]
    public async Task Concurrent_changes_never_lose_an_audit_entry()
    {
        await using var w = await TestWorld.NewAsync();
        for (var i = 0; i < 25; i++)
            await w.Admin.DefineFlagAsync(FlagDefinition.Boolean($"f-{i}", enabled: false), "seed");

        var admins = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => w.NewAdminAsync()));
        await Task.WhenAll(Enumerable.Range(0, 25).Select(i => admins[i].SetEnabledAsync($"f-{i}", true, $"admin-{i}")));

        var audit = await w.Admin.GetAuditAsync();
        // 25 defines + 25 toggles = 50 entries, with a contiguous 1..50 sequence (nothing clobbered).
        Assert.Equal(50, audit.Count);
        Assert.Equal(Enumerable.Range(1, 50).Select(x => (long)x), audit.Select(a => a.Seq).OrderBy(x => x));
    }
}
