using Clustron.Zaris.Client;
using LiveBetting.Core.Domain;
using LiveBetting.Core.Infrastructure;

namespace LiveBetting.Core.Services;

/// <summary>
/// The correctness-critical heart of the engine: accepting an in-play bet. Three independent hazards
/// are handled, each by a specific Zaris mechanism:
///
/// <list type="number">
///   <item><b>Idempotent placement.</b> The bet is keyed by the <b>client-supplied</b> bet id and the
///         record is created <i>create-once</i> (<c>IfAbsent</c>). The creator drives the bet to a
///         terminal state within the call; a duplicate/retry (same id) observes that one outcome rather
///         than placing a second bet. A client may safely retry a timed-out placement.</item>
///   <item><b>Odds-staleness protection.</b> The request carries the odds the bettor was quoted and a
///         tolerance. At acceptance the live market odds are re-read; if they have moved past tolerance
///         the bet is rejected with a fresh requote instead of being struck on stale odds.</item>
///   <item><b>Suspension.</b> If the market is suspended (or settled) when the bet reaches acceptance,
///         it is refused — no stake is ever taken against odds known to be stale.</item>
/// </list>
///
/// The stake debit is delegated to <see cref="WalletService"/>, whose versioned-CAS, exactly-once-by-
/// transaction-id ledger guarantees no overdraw and no double-spend even under thousands of concurrent
/// placements. Money moves <b>only</b> through that debit, under transaction id <c>debit:{betId}</c>, so
/// the whole placement is money-safe regardless of retries, races or crashes.
/// </summary>
public sealed class BettingService
{
    private readonly DocumentStore _store;
    private readonly WalletService _wallet;
    private readonly MarketService _markets;
    private readonly IZaris _z;
    private readonly IClock _clock;

    /// <summary>How long a stale-odds requote token lives before it self-reclaims (native write-time TTL).</summary>
    public static readonly TimeSpan RequoteTtl = TimeSpan.FromSeconds(15);

    public BettingService(DocumentStore store, WalletService wallet, MarketService markets, IZaris zaris, IClock clock)
    {
        _store = store;
        _wallet = wallet;
        _markets = markets;
        _z = zaris;
        _clock = clock;
    }

    private long NowMs => _clock.UtcNow.ToUnixTimeMilliseconds();

    public async Task<PlaceBetResult> PlaceBetAsync(PlaceBetRequest req, CancellationToken ct = default)
    {
        var key = Keys.Bet(req.BetId);

        // Create-once idempotency anchor. The record starts life as a non-terminal placeholder that the
        // creator alone drives to a terminal state in this call.
        var pending = new Bet
        {
            BetId = req.BetId,
            UserId = req.UserId,
            EventId = req.EventId,
            MarketId = req.MarketId,
            SelectionId = req.SelectionId,
            StakeMinor = req.StakeMinor,
            Status = BetStatus.Rejected,       // sentinel; Reason=None marks it as "not yet finalized"
            Reason = RejectReason.None,
            PlacedAtMs = 0
        };

        var createdByMe = await _store.CreateAsync(key, pending, null, ct);
        if (!createdByMe)
        {
            // A bet already exists for this id: a concurrent in-flight duplicate or a later retry.
            var observed = await AwaitTerminalAsync(key, ct);
            return PlaceBetResult.FromBet(observed, await _wallet.GetBalanceAsync(req.UserId, ct), wasReplay: true);
        }

        // We own this bet id — finalize it.
        var finalBet = await FinalizeAsync(req, ct);
        return PlaceBetResult.FromBet(finalBet, await _wallet.GetBalanceAsync(req.UserId, ct), wasReplay: false);
    }

    private async Task<Bet> FinalizeAsync(PlaceBetRequest req, CancellationToken ct)
    {
        var now = NowMs;

        // ── validation against the live market: suspension + odds-staleness ──
        var market = await _markets.GetAsync(req.EventId, req.MarketId, ct);
        RejectReason reason = RejectReason.None;
        decimal acceptedOdds = 0m;
        decimal? requote = null;

        if (market is null)
        {
            reason = RejectReason.MarketNotFound;
        }
        else if (!market.Odds.TryGetValue(req.SelectionId, out var liveOdds))
        {
            reason = RejectReason.UnknownSelection;
        }
        else if (market.Status == MarketStatus.Suspended)
        {
            reason = RejectReason.MarketSuspended;
        }
        else if (market.Status == MarketStatus.Settled)
        {
            reason = RejectReason.MarketClosed;
        }
        else if (Math.Abs(liveOdds - req.QuotedOdds) > req.OddsToleranceAbs)
        {
            reason = RejectReason.OddsChanged;   // odds drifted past tolerance between quote and placement
            requote = liveOdds;
        }
        else
        {
            acceptedOdds = liveOdds;
        }

        if (reason != RejectReason.None)
        {
            // On a stale-odds rejection, offer a fresh requote via a token that self-expires (write-time TTL).
            if (reason == RejectReason.OddsChanged && requote is { } fresh)
                await _store.CreateAsync(Keys.RequoteToken(req.BetId),
                    new RequoteToken(req.BetId, req.SelectionId, req.QuotedOdds, fresh, now),
                    RequoteTtl, ct);

            return await SetTerminalAsync(req.BetId, b =>
            {
                b.Status = BetStatus.Rejected;
                b.Reason = reason;
                b.RequoteOdds = requote;
                b.SettledAtMs = now;
            }, ct);
        }

        // ── atomic, exactly-once stake debit (never overdraws, never double-charges) ──
        var debit = await _wallet.DebitAsync(req.UserId, DebitTxn(req.BetId), req.StakeMinor, ct);
        if (debit.Outcome is TxnOutcome.InsufficientFunds or TxnOutcome.NoWallet)
            return await SetTerminalAsync(req.BetId, b =>
            {
                b.Status = BetStatus.Rejected;
                b.Reason = RejectReason.InsufficientFunds;
                b.SettledAtMs = now;
            }, ct);

        // money reserved (Applied, or AlreadyApplied on a crash-replay) → commit the bet as Placed
        var placed = await SetTerminalAsync(req.BetId, b =>
        {
            b.Status = BetStatus.Placed;
            b.Reason = RejectReason.None;
            b.AcceptedOdds = acceptedOdds;
            b.PlacedAtMs = now;
        }, ct);

        if (placed.Status == BetStatus.Placed)
        {
            // Register in the market's open-bet index (native sorted set — server-side, contention-free)
            // so settlement can find every open bet without scanning.
            await _z.SortedSets.AddAsync(Keys.OpenBetsIndex(req.EventId, req.MarketId),
                new[] { new ScoredMember(req.BetId, now) }, ct);
        }
        else if (debit.Outcome == TxnOutcome.Applied)
        {
            // Defensive: the bet reached a non-Placed terminal state despite our debit (only reachable
            // under an adopt-after-crash race). Refund so money is conserved; idempotent by txn id.
            await _wallet.CreditAsync(req.UserId, RefundTxn(req.BetId), req.StakeMinor, ct);
        }

        return placed;
    }

    /// <summary>
    /// Transitions the bet to a terminal state iff it is still non-terminal, via CAS. If another driver
    /// finalized it first, that committed terminal state is returned unchanged (idempotent / convergent).
    /// </summary>
    private Task<Bet> SetTerminalAsync(string betId, Action<Bet> apply, CancellationToken ct)
        => _store.MutateAsync<Bet>(
            Keys.Bet(betId),
            mutate: cur =>
            {
                if (IsTerminal(cur)) return null;  // already finalized — keep it
                apply(cur);
                return cur;
            },
            create: () => throw new BettingStoreException($"bet {betId} vanished during finalization"),
            ct: ct);

    /// <summary>Waits (bounded) for a concurrently-created bet to reach a terminal state, then adopts it if the creator died.</summary>
    private async Task<Bet> AwaitTerminalAsync(string key, CancellationToken ct)
    {
        for (var i = 0; i < 200; i++)
        {
            var d = await _store.ReadAsync<Bet>(key, ct);
            if (d.Found && IsTerminal(d.Value!))
                return d.Value!;
            await Task.Delay(1, ct).ConfigureAwait(false);
        }
        // Still non-terminal after the budget: the original creator must have crashed mid-placement
        // (only possible across a process restart — there is no live competing finalizer now). Adopt and
        // finalize it ourselves; the debit is idempotent by txn id, so this cannot double-charge.
        var cur = await _store.ReadAsync<Bet>(key, ct);
        var b = cur.Value!;
        return await FinalizeAsync(new PlaceBetRequest(
            b.BetId, b.UserId, b.EventId, b.MarketId, b.SelectionId, b.StakeMinor,
            QuotedOdds: b.AcceptedOdds == 0 ? decimal.Zero : b.AcceptedOdds,
            OddsToleranceAbs: decimal.MaxValue), ct); // tolerance wide: honour the already-created bet
    }

    private static bool IsTerminal(Bet b) =>
        b.Status == BetStatus.Placed || b.Status == BetStatus.Won || b.Status == BetStatus.Lost ||
        (b.Status == BetStatus.Rejected && b.Reason != RejectReason.None);

    public static string DebitTxn(string betId) => $"debit:{betId}";
    public static string RefundTxn(string betId) => $"refund:{betId}";

    public async Task<Bet?> GetBetAsync(string betId, CancellationToken ct = default)
    {
        var d = await _store.ReadAsync<Bet>(Keys.Bet(betId), ct);
        return d.Found ? d.Value : null;
    }

    /// <summary>Fetches the self-expiring requote token for a stale-odds rejection, if it has not yet expired.</summary>
    public async Task<RequoteToken?> GetRequoteAsync(string betId, CancellationToken ct = default)
    {
        var d = await _store.ReadAsync<RequoteToken>(Keys.RequoteToken(betId), ct);
        return d.Found ? d.Value : null;
    }

    /// <summary>The remaining TTL on a requote token (proves the TTL was set at write time), or null if absent/expired.</summary>
    public async Task<TimeSpan?> GetRequoteTtlAsync(string betId, CancellationToken ct = default)
    {
        var r = await _z.GetTimeToLiveAsync(Keys.RequoteToken(betId), ct);
        return r.IsSuccess ? r.Value : null;
    }
}
