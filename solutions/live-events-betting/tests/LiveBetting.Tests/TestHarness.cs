using LiveBetting.Core;
using LiveBetting.Core.Infrastructure;

namespace LiveBetting.Tests;

/// <summary>
/// Spins up an isolated embedded Zaris store (unique name per test, so tests never share state) wired
/// to a hand-advanceable <see cref="FakeClock"/>. Two engines connected to the same store name share
/// one backing engine — used to prove cross-connection behaviour and crash/replay.
/// </summary>
public static class TestHarness
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static FakeClock Clock() => new(Epoch);

    public static string NewStoreName() => "t-" + Guid.NewGuid().ToString("N");

    public static Task<BettingEngine> NewAsync(FakeClock? clock = null, string? store = null) =>
        BettingEngine.ConnectAsync($"zaris://inproc/{store ?? NewStoreName()}", clock ?? Clock());

    /// <summary>A standard 2-outcome market seeded with a funded user, for most tests.</summary>
    public static async Task<(BettingEngine e, string ev, string mkt)> MarketWithFundedUserAsync(
        FakeClock clock, string user = "alice", long balanceMinor = 100_00, string? store = null)
    {
        var e = await NewAsync(clock, store);
        const string ev = "match-1", mkt = "winner";
        await e.Markets.CreateMarketAsync(ev, mkt, "Match winner",
            new Dictionary<string, decimal> { ["home"] = 2.00m, ["away"] = 3.50m });
        await e.Wallets.EnsureWalletAsync(user, balanceMinor);
        return (e, ev, mkt);
    }
}
