using System.Globalization;

namespace LeaderboardPresence.Core;

/// <summary>
/// Centralises every Zaris key / channel / stream name the solution uses, and the date math that turns a wall-clock
/// instant into the current daily/weekly window key. Keeping this in one place makes the data model auditable.
/// </summary>
internal static class Keys
{
    // ── Leaderboard sorted-set keys (one sorted set per window) ──
    public static string Board(string board, LeaderboardWindow window, DateTimeOffset now) => window switch
    {
        LeaderboardWindow.AllTime => $"lb:{board}:all",
        LeaderboardWindow.Daily => $"lb:{board}:d:{DayStamp(now)}",
        LeaderboardWindow.Weekly => $"lb:{board}:w:{WeekStamp(now)}",
        _ => throw new ArgumentOutOfRangeException(nameof(window))
    };

    public static string DailyBoardForStamp(string board, string dayStamp) => $"lb:{board}:d:{dayStamp}";
    public static string WeeklyBoardForStamp(string board, string weekStamp) => $"lb:{board}:w:{weekStamp}";

    // ── Presence keys ──
    /// <summary>Sorted set: every tracked player, scored by last-heartbeat epoch-milliseconds. Drives range queries + sweep.</summary>
    public const string PresenceIndex = "presence:index";

    /// <summary>Per-player record carrying detail + explicit-status override; written with a native Zaris TTL so it self-reclaims.</summary>
    public static string PresenceKey(string player) => $"presence:p:{player}";

    // ── Pub/sub channels ──
    public static string LeaderboardChannel(string board) => $"lbp.lb.{board}";
    public const string LeaderboardChannelPattern = "lbp.lb.*";
    public const string PresenceChannel = "lbp.presence";

    // ── Audit streams ──
    public static string LeaderboardStream(string board) => $"lbp.stream.lb.{board}";
    public const string PresenceStream = "lbp.stream.presence";

    // ── Date stamps ──
    public static string DayStamp(DateTimeOffset now) => now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static string WeekStamp(DateTimeOffset now)
    {
        var d = now.UtcDateTime;
        var week = ISOWeek.GetWeekOfYear(d);
        var year = ISOWeek.GetYear(d);
        return $"{year:0000}-W{week:00}";
    }
}
