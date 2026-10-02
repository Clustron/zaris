using Clustron.Zaris.Client;
using LiveBetting.Core.Domain;
using LiveBetting.Core.Infrastructure;

namespace LiveBetting.Core.Services;

/// <summary>
/// Owns the live odds/market feed and the market lifecycle (open → suspend/resume → settle). Every
/// transition is a single CAS write on the market document, and every transition both:
/// <list type="bullet">
///   <item><b>publishes</b> a <see cref="MarketEvent"/> on Zaris pub/sub (<c>odds.{event}.{market}</c>)
///         for instant fan-out to connected clients (a WebSocket/SignalR bridge subscribes once), and</item>
///   <item><b>appends</b> the same event to a durable Zaris stream (<c>stream:odds:…</c>) so a late or
///         reconnecting client can replay the exact ordered history — pub/sub is at-most-once, the
///         stream is the durable record.</item>
/// </list>
/// Suspension is the in-play safety valve: while a market is <see cref="MarketStatus.Suspended"/> the
/// betting service refuses bets, so no stake is ever accepted against odds that are known to be stale.
/// </summary>
public sealed class MarketService
{
    private readonly DocumentStore _store;
    private readonly IZaris _z;
    private readonly IClock _clock;

    public MarketService(DocumentStore store, IZaris zaris, IClock clock)
    {
        _store = store;
        _z = zaris;
        _clock = clock;
    }

    private long NowMs => _clock.UtcNow.ToUnixTimeMilliseconds();

    public async Task<Market> CreateMarketAsync(string eventId, string marketId, string name, IReadOnlyDictionary<string, decimal> openingOdds, CancellationToken ct = default)
    {
        var market = await _store.MutateAsync<Market>(
            Keys.Market(eventId, marketId),
            mutate: _ => null, // already exists → leave as-is (idempotent create)
            create: () => new Market
            {
                EventId = eventId,
                MarketId = marketId,
                Name = name,
                Status = MarketStatus.Open,
                Odds = new Dictionary<string, decimal>(openingOdds),
                OddsVersion = 1
            },
            ct: ct);
        return market;
    }

    public async Task<Market?> GetAsync(string eventId, string marketId, CancellationToken ct = default)
    {
        var d = await _store.ReadAsync<Market>(Keys.Market(eventId, marketId), ct);
        return d.Found ? d.Value : null;
    }

    /// <summary>Reprices one or more selections (CAS), bumps the odds version, and emits the change on the feed + stream.</summary>
    public async Task<Market> UpdateOddsAsync(string eventId, string marketId, IReadOnlyDictionary<string, decimal> newOdds, CancellationToken ct = default)
    {
        var m = await _store.MutateAsync<Market>(
            Keys.Market(eventId, marketId),
            mutate: cur =>
            {
                var odds = new Dictionary<string, decimal>(cur.Odds);
                foreach (var kv in newOdds) odds[kv.Key] = kv.Value;
                cur.Odds = odds;
                cur.OddsVersion++;
                return cur;
            },
            create: () => throw new BettingStoreException($"market {eventId}:{marketId} does not exist"),
            ct: ct);

        await EmitAsync(m, MarketEventKind.OddsChanged, ct);
        return m;
    }

    /// <summary>Suspends the market so no bets are accepted on stale odds (e.g. a goal was just scored).</summary>
    public async Task<Market> SuspendAsync(string eventId, string marketId, CancellationToken ct = default)
    {
        var m = await _store.MutateAsync<Market>(
            Keys.Market(eventId, marketId),
            mutate: cur =>
            {
                if (cur.Status == MarketStatus.Settled) return null; // cannot suspend a settled market
                if (cur.Status == MarketStatus.Suspended) return null; // idempotent
                cur.Status = MarketStatus.Suspended;
                return cur;
            },
            create: () => throw new BettingStoreException($"market {eventId}:{marketId} does not exist"),
            ct: ct);

        if (m.Status == MarketStatus.Suspended)
            await EmitAsync(m, MarketEventKind.Suspended, ct);
        return m;
    }

    /// <summary>Reopens a suspended market with repriced odds and emits a resume event.</summary>
    public async Task<Market> ResumeAsync(string eventId, string marketId, IReadOnlyDictionary<string, decimal> newOdds, CancellationToken ct = default)
    {
        var m = await _store.MutateAsync<Market>(
            Keys.Market(eventId, marketId),
            mutate: cur =>
            {
                if (cur.Status == MarketStatus.Settled) return null;
                var odds = new Dictionary<string, decimal>(cur.Odds);
                foreach (var kv in newOdds) odds[kv.Key] = kv.Value;
                cur.Odds = odds;
                cur.OddsVersion++;
                cur.Status = MarketStatus.Open;
                return cur;
            },
            create: () => throw new BettingStoreException($"market {eventId}:{marketId} does not exist"),
            ct: ct);

        if (m.Status == MarketStatus.Open)
            await EmitAsync(m, MarketEventKind.Resumed, ct);
        return m;
    }

    /// <summary>
    /// Marks the market resolved with a winning selection (CAS). Returns the market; the actual payout
    /// of individual bets is the <see cref="SettlementService"/>'s job. Idempotent: re-closing with the
    /// same winner is a no-op; closing with a different winner than one already set is rejected.
    /// </summary>
    public async Task<Market> CloseForSettlementAsync(string eventId, string marketId, string winningSelection, CancellationToken ct = default)
    {
        var m = await _store.MutateAsync<Market>(
            Keys.Market(eventId, marketId),
            mutate: cur =>
            {
                if (cur.Status == MarketStatus.Settled)
                {
                    if (cur.WinningSelection != winningSelection)
                        throw new BettingStoreException($"market {eventId}:{marketId} already settled on '{cur.WinningSelection}', cannot re-settle on '{winningSelection}'");
                    return null; // idempotent
                }
                if (!cur.Odds.ContainsKey(winningSelection))
                    throw new BettingStoreException($"winning selection '{winningSelection}' is not a selection on market {eventId}:{marketId}");
                cur.Status = MarketStatus.Settled;
                cur.WinningSelection = winningSelection;
                return cur;
            },
            create: () => throw new BettingStoreException($"market {eventId}:{marketId} does not exist"),
            ct: ct);

        await EmitAsync(m, MarketEventKind.Settled, ct);
        return m;
    }

    // ── live feed: pub/sub (instant) + stream (durable replay) ──

    /// <summary>Subscribes to one market's live feed. Pass <c>null</c> for both ids to pattern-subscribe to every market.</summary>
    public async Task<IAsyncDisposable> SubscribeAsync(Func<MarketEvent, Task> handler, string? eventId = null, string? marketId = null, CancellationToken ct = default)
    {
        Task OnMessage(ChannelMessage msg)
        {
            var ev = Json.From<MarketEvent>(msg.Payload);
            return ev is null ? Task.CompletedTask : handler(ev);
        }

        if (eventId is null || marketId is null)
            return await _z.PubSub.PSubscribeAsync(new[] { Keys.OddsChannelPattern }, OnMessage, ct);
        return await _z.PubSub.SubscribeAsync(new[] { Keys.OddsChannel(eventId, marketId) }, OnMessage, ct);
    }

    /// <summary>Replays the durable odds-feed history for a market, newest first.</summary>
    public async Task<IReadOnlyList<MarketEvent>> ReadFeedHistoryAsync(string eventId, string marketId, int count, CancellationToken ct = default)
    {
        var range = await _z.Streams.RangeAsync(Keys.OddsStream(eventId, marketId), "-", "+", count, reverse: true, ct);
        if (!range.IsSuccess || range.Value is null) return Array.Empty<MarketEvent>();
        var list = new List<MarketEvent>(range.Value.Count);
        foreach (var e in range.Value)
            foreach (var f in e.Fields)
                if (f.Key == "data" && Json.From<MarketEvent>(f.Value) is { } ev)
                    list.Add(ev);
        return list;
    }

    private async Task EmitAsync(Market m, MarketEventKind kind, CancellationToken ct)
    {
        var ev = new MarketEvent(kind, m.EventId, m.MarketId, m.OddsVersion,
            new Dictionary<string, decimal>(m.Odds), m.WinningSelection, NowMs);
        var bytes = Json.Bytes(ev);
        try
        {
            await _z.PubSub.PublishAsync(Keys.OddsChannel(m.EventId, m.MarketId), bytes, ct);
            await _z.Streams.AddAsync(Keys.OddsStream(m.EventId, m.MarketId),
                new[] { new KeyValuePair<string, byte[]>("data", bytes) }, "*", 50_000, null, false, ct);
        }
        catch
        {
            // Feed propagation is best-effort; a publish/stream hiccup must not fail a committed transition.
        }
    }
}
