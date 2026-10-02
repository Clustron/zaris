namespace LiveBetting.Core.Infrastructure;

/// <summary>
/// Every Zaris key, channel and stream name the engine uses, in one auditable place.
///
/// Document keys (versioned KV, CAS):
///   wallet:{userId}                       → the user's wallet (balance + applied-txn ledger)
///   market:{eventId}:{marketId}           → a market (selections+odds, status, winner)
///   bet:{betId}                           → a single bet record (idempotency anchor + lifecycle)
///   settle:{betId}                        → create-once durable settlement-log marker (exactly-once proof)
///
/// Native sorted set (server-side, contention-free index):
///   idx:open:{eventId}:{marketId}         → betIds of still-open bets on a market (member=betId, score=placedAt)
///
/// Pub/sub channels (live fan-out to WebSocket/SignalR bridges):
///   odds.{eventId}.{marketId}             → odds changes + suspend/resume/settle for one market
///   odds.*                                → pattern subscription for every market's feed
///
/// Audit / durable replay streams (append-only, id-ordered):
///   stream:odds:{eventId}:{marketId}      → every odds/suspend/resume event (durable replay of the feed)
///   stream:settle:{eventId}:{marketId}    → every settlement decision (the durable settlement log)
/// </summary>
public static class Keys
{
    public static string Wallet(string userId) => $"wallet:{userId}";
    public static string Market(string eventId, string marketId) => $"market:{eventId}:{marketId}";
    public static string Bet(string betId) => $"bet:{betId}";
    public static string SettleMarker(string betId) => $"settle:{betId}";

    /// <summary>Short-lived requote token written with a native write-time TTL when a bet is rejected for stale odds.</summary>
    public static string RequoteToken(string betId) => $"requote:{betId}";

    public static string OpenBetsIndex(string eventId, string marketId) => $"idx:open:{eventId}:{marketId}";

    public static string OddsChannel(string eventId, string marketId) => $"odds.{eventId}.{marketId}";
    public const string OddsChannelPattern = "odds.*";

    public static string OddsStream(string eventId, string marketId) => $"stream:odds:{eventId}:{marketId}";
    public static string SettlementStream(string eventId, string marketId) => $"stream:settle:{eventId}:{marketId}";
}
