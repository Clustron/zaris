namespace LeaderboardPresence.Core;

/// <summary>Tunables for <see cref="LeaderboardService"/>.</summary>
public sealed class LeaderboardOptions
{
    /// <summary>How long a daily window is retained before <see cref="LeaderboardService.ReapExpiredWindowsAsync"/> deletes it.</summary>
    public TimeSpan DailyRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How long a weekly window is retained before reaping.</summary>
    public TimeSpan WeeklyRetention { get; set; } = TimeSpan.FromDays(60);

    /// <summary>When submitting a score, also mirror it into the current daily and weekly windows.</summary>
    public bool WriteToRollingWindows { get; set; } = true;

    /// <summary>Publish a <see cref="LeaderboardChange"/> on pub/sub and append to the audit stream on every submit.</summary>
    public bool PublishChanges { get; set; } = true;

    /// <summary>Cap the audit stream to this many newest entries per board (0 = unbounded).</summary>
    public long AuditStreamMaxLen { get; set; } = 10_000;
}

/// <summary>Tunables for <see cref="PresenceService"/>. Presence state is derived from the age of the last heartbeat.</summary>
public sealed class PresenceOptions
{
    /// <summary>A player seen within this window is <see cref="PresenceStatus.Online"/>.</summary>
    public TimeSpan OnlineWindow { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>A player seen within this window (but beyond <see cref="OnlineWindow"/>) is <see cref="PresenceStatus.Away"/>; beyond it they are offline.</summary>
    public TimeSpan AwayWindow { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Grace added to <see cref="AwayWindow"/> for the native Zaris TTL on each per-player presence key, so the
    /// key auto-reclaims shortly after the player is considered offline (server-side expiry / memory reclaim).
    /// </summary>
    public TimeSpan TtlGrace { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Publish a <see cref="PresenceChange"/> on pub/sub and append to the audit stream on every transition.</summary>
    public bool PublishChanges { get; set; } = true;

    internal TimeSpan KeyTtl => AwayWindow + TtlGrace;
}
