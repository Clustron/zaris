namespace LeaderboardPresence.Core;

/// <summary>
/// Tracks player presence (online / away / offline) from heartbeats. Every tracked player lives in a Zaris sorted set
/// scored by the epoch-millisecond of their last heartbeat, so "who is online" is an O(log n) score-range query and
/// expiry is a range-and-remove sweep. Each heartbeat also writes a per-player record carrying a native Zaris TTL, so
/// the server reclaims the record automatically once the player goes quiet. Transitions fan out over Zaris pub/sub and
/// a durable stream.
/// </summary>
public interface IPresenceService
{
    /// <summary>Record a heartbeat (player is active now). Optional <paramref name="detail"/> (e.g. device, room). Returns the resulting snapshot.</summary>
    Task<PresenceSnapshot> HeartbeatAsync(string player, string? detail = null, CancellationToken ct = default);

    /// <summary>User-initiated status change (manual Away, or Offline on logout). Returns the resulting snapshot.</summary>
    Task<PresenceSnapshot> SetStatusAsync(string player, PresenceStatus status, string? detail = null, CancellationToken ct = default);

    /// <summary>A player's current snapshot (status derived from last-heartbeat age against the clock).</summary>
    Task<PresenceSnapshot> GetAsync(string player, CancellationToken ct = default);

    /// <summary>All players currently in the given status.</summary>
    Task<IReadOnlyList<PresenceSnapshot>> ListByStatusAsync(PresenceStatus status, CancellationToken ct = default);

    /// <summary>All currently-online players.</summary>
    Task<IReadOnlyList<PresenceSnapshot>> ListOnlineAsync(CancellationToken ct = default);

    /// <summary>Fast count of currently-online players.</summary>
    Task<long> OnlineCountAsync(CancellationToken ct = default);

    /// <summary>Reap players who have gone offline (last heartbeat older than the away window). Emits an Offline event per reaped player. Returns the reaped player names.</summary>
    Task<IReadOnlyList<string>> SweepAsync(CancellationToken ct = default);

    /// <summary>Subscribe to live presence transitions. Dispose to stop.</summary>
    Task<IAsyncDisposable> SubscribeAsync(Func<PresenceChange, Task> handler, CancellationToken ct = default);

    /// <summary>Replay the most recent <paramref name="count"/> presence events from the durable audit stream (newest first).</summary>
    Task<IReadOnlyList<PresenceChange>> ReadAuditAsync(int count, CancellationToken ct = default);
}
