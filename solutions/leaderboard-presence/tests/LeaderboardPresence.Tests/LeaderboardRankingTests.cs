using LeaderboardPresence.Core;
using Xunit;

namespace LeaderboardPresence.Tests;

public class LeaderboardRankingTests
{
    [Fact]
    public async Task Top_returns_players_in_descending_score_order_with_one_based_ranks()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SetScoreAsync("arena", "alice", 100);
        await lb.SetScoreAsync("arena", "bob", 250);
        await lb.SetScoreAsync("arena", "carol", 175);

        var top = await lb.GetTopAsync("arena", 10);

        Assert.Equal(3, top.Count);
        Assert.Equal(new[] { "bob", "carol", "alice" }, top.Select(e => e.Player));
        Assert.Equal(new[] { 1L, 2L, 3L }, top.Select(e => e.Rank));
        Assert.Equal(250, top[0].Score);
    }

    [Fact]
    public async Task Ties_are_broken_lexicographically_by_member()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        // Equal scores: Redis/Zaris order ascending member within equal score, so reverse (top) order is zed..alf.
        await lb.SetScoreAsync("arena", "mike", 50);
        await lb.SetScoreAsync("arena", "zed", 50);
        await lb.SetScoreAsync("arena", "alf", 50);

        var top = await lb.GetTopAsync("arena", 10);

        Assert.Equal(new[] { "zed", "mike", "alf" }, top.Select(e => e.Player));
        Assert.All(top, e => Assert.Equal(50, e.Score));
        Assert.Equal(new[] { 1L, 2L, 3L }, top.Select(e => e.Rank));
    }

    [Fact]
    public async Task SubmitScore_accumulates_atomically()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SubmitScoreAsync("arena", "alice", 10);
        await lb.SubmitScoreAsync("arena", "alice", 15);
        var r = await lb.SubmitScoreAsync("arena", "alice", 5);

        Assert.Equal(30, r.NewScore);
        Assert.Equal(1, r.NewRank);
        var rank = await lb.GetRankAsync("arena", "alice");
        Assert.NotNull(rank);
        Assert.Equal(30, rank!.Score);
    }

    [Fact]
    public async Task GetRank_is_one_based_and_null_for_unknown_player()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SetScoreAsync("arena", "a", 30);
        await lb.SetScoreAsync("arena", "b", 20);
        await lb.SetScoreAsync("arena", "c", 10);

        Assert.Equal(1, (await lb.GetRankAsync("arena", "a"))!.Rank);
        Assert.Equal(2, (await lb.GetRankAsync("arena", "b"))!.Rank);
        Assert.Equal(3, (await lb.GetRankAsync("arena", "c"))!.Rank);
        Assert.Null(await lb.GetRankAsync("arena", "ghost"));
    }

    [Fact]
    public async Task GetNeighbors_returns_player_and_surrounding_ranks()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        for (var i = 0; i < 10; i++)
            await lb.SetScoreAsync("arena", $"p{i:00}", (10 - i) * 100); // p00=1000 (rank1) .. p09=100 (rank10)

        var neighbors = await lb.GetNeighborsAsync("arena", "p04", radius: 2); // rank 5

        Assert.Equal(new[] { "p02", "p03", "p04", "p05", "p06" }, neighbors.Select(e => e.Player));
        Assert.Equal(new[] { 3L, 4L, 5L, 6L, 7L }, neighbors.Select(e => e.Rank));
    }

    [Fact]
    public async Task GetNeighbors_clamps_at_the_top_edge()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        for (var i = 0; i < 5; i++)
            await lb.SetScoreAsync("arena", $"p{i}", (5 - i) * 10);

        var neighbors = await lb.GetNeighborsAsync("arena", "p0", radius: 3); // top player

        Assert.Equal("p0", neighbors[0].Player);
        Assert.Equal(1L, neighbors[0].Rank);
        Assert.Equal(4, neighbors.Count); // self + 3 below
    }

    [Fact]
    public async Task Count_reflects_distinct_members()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        await lb.SubmitScoreAsync("arena", "alice", 5);
        await lb.SubmitScoreAsync("arena", "alice", 5); // same member again
        await lb.SubmitScoreAsync("arena", "bob", 5);

        Assert.Equal(2, await lb.GetCountAsync("arena"));
    }

    [Fact]
    public async Task Empty_board_reads_are_empty_not_errors()
    {
        var clock = TestHarness.Clock();
        await using var sys = await TestHarness.NewAsync(clock);
        var lb = sys.Leaderboard;

        Assert.Empty(await lb.GetTopAsync("ghost", 10));
        Assert.Equal(0, await lb.GetCountAsync("ghost"));
        Assert.Null(await lb.GetRankAsync("ghost", "nobody"));
        Assert.Empty(await lb.GetNeighborsAsync("ghost", "nobody", 5));
    }
}
