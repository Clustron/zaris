using LeaderboardPresence.Core;

namespace LeaderboardPresence.Tests;

/// <summary>
/// Spins up an isolated embedded Zaris store (unique name per test, so tests never share state) wired to a
/// hand-advanceable <see cref="FakeClock"/>, so presence windows and leaderboard window-keys are deterministic.
/// </summary>
public static class TestHarness
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static Task<LeaderboardPresenceSystem> NewAsync(
        FakeClock clock,
        LeaderboardOptions? lb = null,
        PresenceOptions? pr = null)
    {
        var store = "t-" + Guid.NewGuid().ToString("N");
        return LeaderboardPresenceSystem.ConnectAsync(
            $"zaris://inproc/{store}",
            lb ?? new LeaderboardOptions(),
            pr ?? new PresenceOptions(),
            clock);
    }

    public static FakeClock Clock() => new(Epoch);
}
