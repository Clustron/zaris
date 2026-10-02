namespace LeaderboardPresence.Core;

/// <summary>Which rolling window a leaderboard read/write targets. Each window is a separate Zaris sorted set.</summary>
public enum LeaderboardWindow
{
    /// <summary>Never rolls over — the canonical all-time board.</summary>
    AllTime,
    /// <summary>One sorted set per UTC calendar day (key suffix <c>yyyyMMdd</c>).</summary>
    Daily,
    /// <summary>One sorted set per ISO-8601 week (key suffix <c>yyyy-Www</c>).</summary>
    Weekly
}

/// <summary>Derived presence state. Computed from how long ago the player last heart-beat.</summary>
public enum PresenceStatus
{
    Online,
    Away,
    Offline
}

/// <summary>One ranked row on a leaderboard. <see cref="Rank"/> is 1-based (1 == top).</summary>
public sealed record RankedEntry(string Player, double Score, long Rank);

/// <summary>Outcome of a score submission against the all-time board.</summary>
public sealed record SubmitResult(string Player, double NewScore, long NewRank, double Delta);

/// <summary>A snapshot of a player's presence at a point in time.</summary>
public sealed record PresenceSnapshot(string Player, PresenceStatus Status, DateTimeOffset LastSeenUtc, string? Detail);

/// <summary>Event published whenever a player's leaderboard position changes. Carried over pub/sub and the audit stream.</summary>
public sealed record LeaderboardChange(
    string Board,
    string Window,
    string Player,
    double NewScore,
    long NewRank,
    double Delta,
    DateTimeOffset TimestampUtc);

/// <summary>Event published whenever a player transitions presence state.</summary>
public sealed record PresenceChange(
    string Player,
    PresenceStatus Status,
    PresenceStatus PreviousStatus,
    string? Detail,
    DateTimeOffset TimestampUtc);
