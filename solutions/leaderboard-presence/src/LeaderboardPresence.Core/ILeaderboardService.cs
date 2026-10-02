namespace LeaderboardPresence.Core;

/// <summary>
/// A real-time leaderboard backed by Zaris sorted sets. Submissions are atomic score increments (linearizable even
/// under heavy concurrency), reads are O(log n) rank/range queries, and every change is fanned out over Zaris pub/sub
/// plus appended to a durable Zaris stream for replay.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>Atomically add <paramref name="delta"/> to a player's score (across the all-time + current rolling windows). Returns the new all-time score and 1-based rank.</summary>
    Task<SubmitResult> SubmitScoreAsync(string board, string player, double delta, CancellationToken ct = default);

    /// <summary>Set a player's absolute score (across windows). Returns the new all-time score and 1-based rank.</summary>
    Task<SubmitResult> SetScoreAsync(string board, string player, double score, CancellationToken ct = default);

    /// <summary>Top <paramref name="count"/> players, highest score first, ties broken lexicographically. Ranks are 1-based.</summary>
    Task<IReadOnlyList<RankedEntry>> GetTopAsync(string board, int count, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default);

    /// <summary>A single player's score and 1-based rank, or null if they are not on the board.</summary>
    Task<RankedEntry?> GetRankAsync(string board, string player, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default);

    /// <summary>The player plus up to <paramref name="radius"/> neighbours on each side, highest first, ranks 1-based.</summary>
    Task<IReadOnlyList<RankedEntry>> GetNeighborsAsync(string board, string player, int radius, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default);

    /// <summary>Number of players on the board/window.</summary>
    Task<long> GetCountAsync(string board, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default);

    /// <summary>Delete rolling-window sorted sets older than the configured retention. Returns how many keys were removed.</summary>
    Task<int> ReapExpiredWindowsAsync(string board, CancellationToken ct = default);

    /// <summary>Subscribe to live leaderboard changes. <paramref name="board"/> null = all boards (pattern subscribe). Dispose to stop.</summary>
    Task<IAsyncDisposable> SubscribeChangesAsync(Func<LeaderboardChange, Task> handler, string? board = null, CancellationToken ct = default);

    /// <summary>Replay the most recent <paramref name="count"/> change events from the durable audit stream (newest first).</summary>
    Task<IReadOnlyList<LeaderboardChange>> ReadAuditAsync(string board, int count, CancellationToken ct = default);
}
