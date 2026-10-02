namespace LiveBetting.Core.Domain;

/// <summary>The kind of thing that happened to a market, carried on the live feed and the audit stream.</summary>
public enum MarketEventKind
{
    OddsChanged,
    Suspended,
    Resumed,
    Settled
}

/// <summary>
/// A live market event, published on Zaris pub/sub (<c>odds.{event}.{market}</c>) for instant fan-out
/// to connected clients and appended to the durable Zaris stream (<c>stream:odds:…</c>) so a late or
/// reconnecting client can replay exactly what changed and in what order.
/// </summary>
public sealed record MarketEvent(
    MarketEventKind Kind,
    string EventId,
    string MarketId,
    long OddsVersion,
    IReadOnlyDictionary<string, decimal> Odds,
    string? WinningSelection,
    long AtMs);

/// <summary>
/// A short-lived requote offer handed back when a bet is rejected because the odds moved. It is stored
/// with a <b>native write-time TTL</b> (<see cref="Clustron.Zaris.Abstractions.PutOptions.Metadata"/>.Ttl)
/// so it self-reclaims if the client never comes back to re-quote — no sweeper, no delete call.
/// </summary>
public sealed record RequoteToken(
    string BetId,
    string SelectionId,
    decimal QuotedOdds,
    decimal FreshOdds,
    long AtMs);

/// <summary>One entry in the durable settlement log (the <c>stream:settle:…</c> stream): the exactly-once record of how a single bet resolved.</summary>
public sealed record SettlementRecord(
    string BetId,
    string UserId,
    string EventId,
    string MarketId,
    string SelectionId,
    string WinningSelection,
    bool Won,
    long StakeMinor,
    decimal AcceptedOdds,
    long PayoutMinor,
    long AtMs);
