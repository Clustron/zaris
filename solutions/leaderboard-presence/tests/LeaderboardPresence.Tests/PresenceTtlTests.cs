using Clustron.Zaris.Abstractions;
using LeaderboardPresence.Core;
using Xunit;

namespace LeaderboardPresence.Tests;

/// <summary>
/// Proves the per-player presence record carries a real, server-honoured Zaris TTL (written at PUT time via
/// <c>PutOptions.Metadata.Ttl</c>) so the store reclaims it automatically once a player stops heart-beating.
/// Uses the real clock + a short TTL (not the FakeClock), so it exercises native expiry end-to-end.
/// </summary>
public class PresenceTtlTests
{
    [Fact]
    public async Task Per_player_presence_key_expires_natively_after_the_ttl()
    {
        var opts = new PresenceOptions
        {
            OnlineWindow = TimeSpan.FromMilliseconds(150),
            AwayWindow = TimeSpan.FromMilliseconds(300),
            TtlGrace = TimeSpan.FromMilliseconds(100) // KeyTtl = 400ms
        };
        await using var sys = await LeaderboardPresenceSystem.ConnectAsync(
            $"zaris://inproc/ttl-{Guid.NewGuid():N}", presenceOptions: opts, clock: SystemClock.Instance);

        await sys.Presence.HeartbeatAsync("alice", "console");

        var key = "presence:p:alice";
        var immediately = await sys.Zaris.GetAsync<byte[]>(key);
        Assert.True(immediately.IsSuccess, "presence key should exist right after a heartbeat");

        var ttl = await sys.Zaris.GetTimeToLiveAsync(key);
        Assert.True(ttl.IsSuccess && ttl.Value is { } t && t > TimeSpan.Zero, "presence key should carry a positive TTL");

        await Task.Delay(700); // exceed the 400ms TTL

        var afterExpiry = await sys.Zaris.GetAsync<byte[]>(key);
        Assert.Equal(KvStatus.NotFound, afterExpiry.Status);
    }
}
