namespace LiveBetting.Core.Domain;

// ─────────────────────────────────────────────────────────────────────────────
// Wallet
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A user's money account. The invariant the whole engine protects: <see cref="BalanceMinor"/> is
/// never negative, and every change to it is applied <b>exactly once</b> per transaction id.
///
/// <see cref="AppliedTxns"/> is the idempotency ledger: each debit (<c>debit:{betId}</c>) and each
/// payout credit (<c>payout:{betId}</c>) carries a unique transaction id, and the id is recorded in
/// the same CAS write that moves the balance. A retried or replayed transaction whose id is already
/// present is a no-op, so crashes, client retries and settlement re-runs can never double-charge or
/// double-pay. (For the demo the ledger grows unbounded; a production system would compact settled
/// ids out of band — see the README.)
/// </summary>
public sealed class Wallet
{
    public string UserId { get; set; } = "";
    public long BalanceMinor { get; set; }
    public HashSet<string> AppliedTxns { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Market / odds
// ─────────────────────────────────────────────────────────────────────────────

public enum MarketStatus
{
    /// <summary>Accepting bets at the current odds.</summary>
    Open,
    /// <summary>Temporarily not accepting bets (e.g. a goal was scored and odds are being repriced).</summary>
    Suspended,
    /// <summary>Resolved; a winning selection is set and bets have been (or are being) settled.</summary>
    Settled
}

/// <summary>
/// A market on an event (e.g. "match odds" on a football match): a set of selections each with its
/// current decimal odds, a lifecycle status, and — once resolved — the winning selection. Stored as a
/// single CAS document so an odds change, a suspension and a settlement are each one atomic transition.
/// </summary>
public sealed class Market
{
    public string EventId { get; set; } = "";
    public string MarketId { get; set; } = "";
    public string Name { get; set; } = "";
    public MarketStatus Status { get; set; } = MarketStatus.Open;
    /// <summary>selectionId → current decimal odds.</summary>
    public Dictionary<string, decimal> Odds { get; set; } = new();
    public string? WinningSelection { get; set; }
    /// <summary>Monotonic counter bumped on every odds change; lets subscribers detect they missed a tick.</summary>
    public long OddsVersion { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Bet
// ─────────────────────────────────────────────────────────────────────────────

public enum BetStatus
{
    /// <summary>Accepted and staked; awaiting settlement.</summary>
    Placed,
    /// <summary>Not accepted (see <see cref="Bet.Reason"/>); no money moved.</summary>
    Rejected,
    /// <summary>Settled as a winner; payout credited.</summary>
    Won,
    /// <summary>Settled as a loser; stake retained by the house.</summary>
    Lost
}

public enum RejectReason
{
    None,
    MarketNotFound,
    UnknownSelection,
    MarketSuspended,
    MarketClosed,
    OddsChanged,
    InsufficientFunds
}

/// <summary>
/// A single bet, keyed by the <b>client-supplied</b> bet id (the idempotency key). The record is both
/// the business entity and the dedupe anchor: a retry with the same id returns this record unchanged
/// rather than placing a second bet. A bet is terminal in exactly one of {Placed→(Won|Lost), Rejected}.
/// </summary>
public sealed class Bet
{
    public string BetId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string EventId { get; set; } = "";
    public string MarketId { get; set; } = "";
    public string SelectionId { get; set; } = "";
    public long StakeMinor { get; set; }
    /// <summary>The odds the bet was actually accepted at (the live odds at acceptance, within tolerance of the quote).</summary>
    public decimal AcceptedOdds { get; set; }
    public BetStatus Status { get; set; }
    public RejectReason Reason { get; set; }
    /// <summary>On an OddsChanged rejection, the fresh odds to requote the client at.</summary>
    public decimal? RequoteOdds { get; set; }
    public long PayoutMinor { get; set; }
    public long PlacedAtMs { get; set; }
    public long SettledAtMs { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Requests / results (the public surface)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A placement request. <see cref="BetId"/> is the idempotency key the caller controls. <see cref="QuotedOdds"/>
/// are the odds the bettor was shown; <see cref="OddsToleranceAbs"/> is how far the live odds may have
/// moved (absolute, in odds points) before the bet must be requoted instead of accepted.
/// </summary>
public sealed record PlaceBetRequest(
    string BetId,
    string UserId,
    string EventId,
    string MarketId,
    string SelectionId,
    long StakeMinor,
    decimal QuotedOdds,
    decimal OddsToleranceAbs = 0.10m);

/// <summary>The outcome of a placement call. <see cref="Accepted"/> is true only when a stake was debited and a bet was placed.</summary>
public sealed record PlaceBetResult(
    bool Accepted,
    BetStatus Status,
    RejectReason Reason,
    string BetId,
    long StakeMinor,
    decimal AcceptedOdds,
    decimal? RequoteOdds,
    long WalletBalanceMinor,
    bool WasReplay)
{
    public static PlaceBetResult FromBet(Bet b, long balance, bool wasReplay) => new(
        Accepted: b.Status == BetStatus.Placed,
        Status: b.Status,
        Reason: b.Reason,
        BetId: b.BetId,
        StakeMinor: b.StakeMinor,
        AcceptedOdds: b.AcceptedOdds,
        RequoteOdds: b.RequoteOdds,
        WalletBalanceMinor: balance,
        WasReplay: wasReplay);
}
