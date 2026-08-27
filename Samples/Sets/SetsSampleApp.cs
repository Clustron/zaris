using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Sets;

internal class SetsSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public SetsSampleApp(IZarisClientProvider provider)
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

        var context = new SampleContext("sets");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // A Zaris set is an unordered collection of unique string members —
        // the native equivalent of a Redis set (SADD/SREM/SMEMBERS/…).
        // Here we model the tags on an article.
        var sets = ((IZaris)client).Sets;

        var tagsKey = context.Key("article:1:tags");

        // ============================================================
        // Add members
        // ============================================================

        ConsoleHelper.Section("Add members");

        var added = await sets.AddAsync(
            tagsKey,
            new[] { "distributed-systems", "dotnet", "cache", "redis" });

        if (added.IsSuccess)
            ConsoleHelper.Success($"Added {added.Value} tags.");
        else
            ConsoleHelper.Error($"ADD failed: {added.Status}");

        // ============================================================
        // Duplicates are ignored (set semantics)
        // ============================================================

        ConsoleHelper.Section("Add a duplicate");

        var dup = await sets.AddAsync(tagsKey, new[] { "dotnet" });
        Console.WriteLine($"Adding an existing member → newly added: {dup.Value} (0 = already present).");

        // ============================================================
        // Members
        // ============================================================

        ConsoleHelper.Section("Members");

        var members = await sets.MembersAsync(tagsKey);
        if (members.IsSuccess)
        {
            Console.WriteLine($"Members ({members.Value.Count}):");
            foreach (var m in members.Value)
                Console.WriteLine($"  - {m}");
        }
        else
        {
            ConsoleHelper.Error($"MEMBERS failed: {members.Status}");
        }

        // ============================================================
        // Membership test
        // ============================================================

        ConsoleHelper.Section("Contains");

        var hasDotnet = await sets.ContainsAsync(tagsKey, "dotnet");
        var hasJava = await sets.ContainsAsync(tagsKey, "java");
        Console.WriteLine($"Contains 'dotnet' → {hasDotnet.Value}");
        Console.WriteLine($"Contains 'java'   → {hasJava.Value}");

        // ============================================================
        // Count
        // ============================================================

        ConsoleHelper.Section("Count");

        var count = await sets.CountAsync(tagsKey);
        ConsoleHelper.Success($"Set cardinality: {count.Value}");

        // ============================================================
        // Random members (non-destructive)
        // ============================================================

        ConsoleHelper.Section("Random members");

        var random = await sets.RandomMembersAsync(tagsKey, 2);
        if (random.IsSuccess)
            Console.WriteLine($"2 random tags → {string.Join(", ", random.Value)}");

        // ============================================================
        // Remove a member
        // ============================================================

        ConsoleHelper.Section("Remove");

        var removed = await sets.RemoveAsync(tagsKey, new[] { "redis" });
        Console.WriteLine($"Removed 'redis' → removed count: {removed.Value}");

        var after = await sets.MembersAsync(tagsKey);
        if (after.IsSuccess)
            Console.WriteLine($"Members now ({after.Value.Count}): {string.Join(", ", after.Value)}");

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}
