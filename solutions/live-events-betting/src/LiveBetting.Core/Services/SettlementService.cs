using Clustron.Zaris.Client;
using LiveBetting.Core.Domain;
using LiveBetting.Core.Infrastructure;

namespace LiveBetting.Core.Services;

public sealed record SettlementSummary(
    string EventId,
    string MarketId,
    string WinningSelection,
    int Settled,
    int Won,
    int Lost,
    int AlreadySettled,
    long TotalStakeMinor,
    long TotalPayoutMinor);

/// <summary>
/// Settles every open bet on a resolved market <b>exactly once</b>, and is safe to re-run after a crash.
///
/// Three layers make this exactly-once:
/// <list type="bullet">
///   <item><b>Order of operations.</b> For a winner the payout is credited <i>before</i> the bet is
///         marked <see cref="BetStatus.Won"/>. The credit is idempotent (transaction id
///         <c>payout:{betId}</c>), so a crash between crediting and marking simply re-credits as a no-op
///         on replay and then marks the bet — a winner can never be paid twice nor missed.</item>
///   <item><b>Terminal-state gate.</b> Only bets still in <see cref="BetStatus.Placed"/> are processed;
///         a bet already Won/Lost is skipped, so a re-run touches no money.</item>
///   <item><b>Durable settlement log.</b> Each decision is written create-once to <c>settle:{betId}</c>
///         and appended to a durable Zaris stream (<c>stream:settle:…</c>) — the auditable, replayable
///         record that settlement happened and how.</item>
/// </list>
/// Re-running <see cref="SettleMarketAsync"/> (same winner) therefore pays nobody twice: the market
/// close is idempotent, every settled bet is skipped, and every payout credit is a no-op.
/// </summary>
public sealed class SettlementService
{
    private readonly DocumentStore _store;
    private readonly WalletService _wallet;
    private readonly MarketService _markets;
    private readonly IZaris _z;
    private readonly IClock _clock;

    public SettlementService(DocumentStore store, WalletService wallet, MarketService markets, IZaris zaris, IClock clock)
    {
        _store = store;
        _wallet = wallet;
        _markets = markets;
        _z = zaris;
        _clock = clock;
    }

    private long NowMs => _clock.UtcNow.ToUnixTimeMilliseconds();

    public async Task<SettlementSummary> SettleMarketAsync(string eventId, string marketId, string winningSelection, CancellationToken ct = default)
    {
        // 1) Resolve the market (idempotent CAS; also emits a Settled event on the feed + stream).
        await _markets.CloseForSettlementAsync(eventId, marketId, winningSelection, ct);

        // 2) Enumerate the open-bet index for this market.
        var idxKey = Keys.OpenBetsIndex(eventId, marketId);
        var open = await _z.SortedSets.RangeByRankAsync(idxKey, 0, -1, reverse: false, ct);
        var betIds = open.IsSuccess && open.Value is not null
            ? open.Value.Select(m => m.Member).ToList()
            : new List<string>();

        int settled = 0, won = 0, lost = 0, already = 0;
        long totalStake = 0, totalPayout = 0;

        foreach (var betId in betIds)
        {
            var d = await _store.ReadAsync<Bet>(Keys.Bet(betId), ct);
            if (!d.Found) { await RemoveFromIndexAsync(idxKey, betId, ct); continue; }
            var bet = d.Value!;

            if (bet.Status != BetStatus.Placed)
            {
                // Already settled on a prior run (or never placed) — nothing to do, just unindex.
                already++;
                await RemoveFromIndexAsync(idxKey, betId, ct);
                continue;
            }

            var isWinner = bet.SelectionId == winningSelection;
            long payout = 0;

            if (isWinner)
            {
                payout = Money.Payout(bet.StakeMinor, bet.AcceptedOdds);
                // Credit BEFORE marking Won (see class doc) — idempotent by txn id.
                await _wallet.CreditAsync(bet.UserId, PayoutTxn(betId), payout, ct);
            }

            var settledBet = await _store.MutateAsync<Bet>(
                Keys.Bet(betId),
                mutate: cur =>
                {
                    if (cur.Status != BetStatus.Placed) return null; // someone settled it concurrently
                    cur.Status = isWinner ? BetStatus.Won : BetStatus.Lost;
                    cur.PayoutMinor = payout;
                    cur.SettledAtMs = NowMs;
                    return cur;
                },
                create: () => throw new BettingStoreException($"bet {betId} vanished during settlement"),
                ct: ct);

            // Durable settlement-log record (create-once) + append to the audit stream.
            var record = new SettlementRecord(betId, bet.UserId, eventId, marketId, bet.SelectionId,
                winningSelection, isWinner, bet.StakeMinor, bet.AcceptedOdds, payout, NowMs);
            await _store.CreateAsync(Keys.SettleMarker(betId), record, null, ct);
            await AppendSettlementStreamAsync(eventId, marketId, record, ct);

            settled++;
            totalStake += bet.StakeMinor;
            if (settledBet.Status == BetStatus.Won) { won++; totalPayout += payout; }
            else lost++;

            await RemoveFromIndexAsync(idxKey, betId, ct);
        }

        return new SettlementSummary(eventId, marketId, winningSelection, settled, won, lost, already, totalStake, totalPayout);
    }

    /// <summary>Replays the durable settlement log for a market, newest first.</summary>
    public async Task<IReadOnlyList<SettlementRecord>> ReadSettlementLogAsync(string eventId, string marketId, int count, CancellationToken ct = default)
    {
        var range = await _z.Streams.RangeAsync(Keys.SettlementStream(eventId, marketId), "-", "+", count, reverse: true, ct);
        if (!range.IsSuccess || range.Value is null) return Array.Empty<SettlementRecord>();
        var list = new List<SettlementRecord>(range.Value.Count);
        foreach (var e in range.Value)
            foreach (var f in e.Fields)
                if (f.Key == "data" && Json.From<SettlementRecord>(f.Value) is { } rec)
                    list.Add(rec);
        return list;
    }

    private async Task AppendSettlementStreamAsync(string eventId, string marketId, SettlementRecord record, CancellationToken ct)
    {
        try
        {
            await _z.Streams.AddAsync(Keys.SettlementStream(eventId, marketId),
                new[] { new KeyValuePair<string, byte[]>("data", Json.Bytes(record)) }, "*", 100_000, null, false, ct);
        }
        catch { /* the stream is an audit mirror; the create-once marker + wallet ledger are authoritative */ }
    }

    private async Task RemoveFromIndexAsync(string idxKey, string betId, CancellationToken ct)
    {
        try { await _z.SortedSets.RemoveAsync(idxKey, new[] { betId }, ct); } catch { /* best-effort */ }
    }

    public static string PayoutTxn(string betId) => $"payout:{betId}";
}
