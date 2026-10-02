namespace LeaderboardPresence.Core;

/// <summary>
/// Abstracts "now" so presence windows and leaderboard window-keys are deterministic under test.
/// Presence status (online/away/offline) is derived from how long ago a player last heart-beat,
/// so the whole subsystem is driven by this single clock.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock — wall time.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Hand-advanceable clock for tests.</summary>
public sealed class FakeClock : IClock
{
    private DateTimeOffset _now;
    public FakeClock(DateTimeOffset start) => _now = start;
    public DateTimeOffset UtcNow => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
    public void Set(DateTimeOffset to) => _now = to;
}
