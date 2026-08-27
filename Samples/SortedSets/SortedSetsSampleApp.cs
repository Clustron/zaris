using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Abstractions.Collections;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.SortedSets;

/// <summary>
/// Demonstrates the native sorted-set data structure (<see cref="ISortedSetClient"/>)
/// by building and querying a game leaderboard: members ranked by score, with
/// rank/score lookups, score bands, and in-place score increments.
/// </summary>
internal class SortedSetsSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public SortedSetsSampleApp(IZarisClientProvider provider)
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

        var context = new SampleContext("sortedsets");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // Sorted sets live on IZaris (cast from IZarisClient).
        var zset = ((IZaris)client).SortedSets;

        var leaderboard = context.Key("leaderboard");

        // ============================================================
        // Add players with scores
        // ============================================================

        ConsoleHelper.Section("Add players with scores");

        var added = await zset.AddAsync(leaderboard, new List<ScoredMember>
        {
            new ScoredMember("ada",     4096),
            new ScoredMember("grace",   3120),
            new ScoredMember("linus",   2750),
            new ScoredMember("margaret", 5010),
            new ScoredMember("dennis",  1980),
        });

        if (added.IsSuccess)
            ConsoleHelper.Success($"Added {added.Value} players to the leaderboard.");
        else
            ConsoleHelper.Error($"Add failed: {added.Status}");

        // ============================================================
        // Score of a single player
        // ============================================================

        ConsoleHelper.Section("Score lookup");

        var score = await zset.ScoreAsync(leaderboard, "linus");
        Console.WriteLine($"linus → score {score.Value?.ToString() ?? "(none)"}");

        // ============================================================
        // Increment a score (player earns points)
        // ============================================================

        ConsoleHelper.Section("Increment a score");

        var bumped = await zset.IncrementAsync(leaderboard, "dennis", 2500);
        Console.WriteLine($"dennis +2500 → new score {bumped.Value}");

        // ============================================================
        // Top ranking (highest score first)
        // ============================================================

        ConsoleHelper.Section("Top ranking");

        var ranking = await zset.RangeByRankAsync(leaderboard, 0, -1, reverse: true);
        if (ranking.IsSuccess)
        {
            var rank = 1;
            foreach (var m in ranking.Value)
                Console.WriteLine($"#{rank++,-2} {m.Member,-10} = {m.Score}");
        }
        else
        {
            ConsoleHelper.Error($"RangeByRank failed: {ranking.Status}");
        }

        // ============================================================
        // Rank of a single player (1st place = rank 0)
        // ============================================================

        ConsoleHelper.Section("Rank lookup");

        var adaRank = await zset.RankAsync(leaderboard, "ada", reverse: true);
        Console.WriteLine($"ada is ranked #{(adaRank.Value.HasValue ? adaRank.Value + 1 : null)} on the board.");

        // ============================================================
        // Score band (players scoring between 2000 and 4200)
        // ============================================================

        ConsoleHelper.Section("Score band 2000–4200");

        var band = await zset.RangeByScoreAsync(leaderboard, 2000, 4200);
        if (band.IsSuccess)
            foreach (var m in band.Value)
                Console.WriteLine($"{m.Member,-10} = {m.Score}");

        var bandCount = await zset.CountByScoreAsync(leaderboard, 2000, 4200);
        Console.WriteLine($"→ {bandCount.Value} players in the 2000–4200 band.");

        // ============================================================
        // Total members
        // ============================================================

        ConsoleHelper.Section("Total members");

        var count = await zset.CountAsync(leaderboard);
        Console.WriteLine($"Leaderboard size: {count.Value}");

        // ============================================================
        // Remove a player
        // ============================================================

        ConsoleHelper.Section("Remove a player");

        var removed = await zset.RemoveAsync(leaderboard, new List<string> { "linus" });
        Console.WriteLine($"Removed {removed.Value} player(s).");

        var finalBoard = await zset.RangeByRankAsync(leaderboard, 0, -1, reverse: true);
        if (finalBoard.IsSuccess)
        {
            ConsoleHelper.Info("Final board:");
            var rank = 1;
            foreach (var m in finalBoard.Value)
                Console.WriteLine($"#{rank++,-2} {m.Member,-10} = {m.Score}");
        }

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}
