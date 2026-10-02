using System.Collections.Concurrent;
using LiveBetting.Core;
using LiveBetting.Core.Domain;

namespace LiveBetting.Demo;

/// <summary>
/// Drives a realistic, genuinely-concurrent in-play betting workload against the engine and injects the
/// classic hazards — odds drift (stale quotes), insufficient funds, suspension during a betting storm,
/// and duplicate submissions (client retries) — collecting the evidence needed to prove money-safety and
/// exactly-once settlement.
///
/// The storm is organised into overlapping waves so that every hazard is reliably exercised, while each
/// wave itself fires all its bets concurrently (so the atomic-debit / idempotency guarantees are under
/// real contention):
///   A. live, fresh odds           → accepted (or insufficient-funds)
///   B. odds drift, clients hold a stale quote → requoted (odds-changed)
///   C. market suspended (goal!)    → refused (suspended)
///   D. resumed at new odds         → accepted (or insufficient-funds)
/// </summary>
public sealed class Simulator
{
    private readonly BettingEngine _engine;
    private readonly Random _rng;

    public Simulator(BettingEngine engine, int seed = 12345)
    {
        _engine = engine;
        _rng = new Random(seed);
    }

    public sealed record Tally
    {
        public int Submissions;       // total placement calls (incl. duplicate retries)
        public int DistinctPlaced;    // distinct bets actually placed (money moved once each)
        public int Accepted;          // accepted results (incl. retry duplicates)
        public int RejStale;
        public int RejInsufficient;
        public int RejSuspended;
        public int RejOther;
        public int Replays;
        public long OpeningFloatMinor;
        public List<string> Users = new();
    }

    public async Task<Tally> RunStormAsync(string ev, string mkt, string[] selections, int bettors, CancellationToken ct = default)
    {
        var tally = new Tally();

        // Fund wallets: 85% comfortably funded, 15% deliberately short so insufficient-funds is exercised.
        for (var i = 0; i < bettors; i++)
        {
            var user = $"user-{i:0000}";
            var funded = _rng.NextDouble() > 0.15;
            var opening = funded ? _rng.Next(50, 200) * 100L : _rng.Next(1, 6) * 100L;
            await _engine.Wallets.EnsureWalletAsync(user, opening, ct);
            tally.OpeningFloatMinor += opening;
            tally.Users.Add(user);
        }

        var results = new ConcurrentBag<PlaceBetResult>();
        var distinctPlaced = new ConcurrentDictionary<string, byte>();
        var betSeq = 0;

        // Snapshot the opening odds — wave B clients will hold this quote while the market drifts under them.
        var market = await _engine.Markets.GetAsync(ev, mkt, ct);
        var staleQuote = new Dictionary<string, decimal>(market!.Odds);

        async Task PlaceWave(IEnumerable<string> users, Func<string, decimal> quoteFor, bool duplicate)
        {
            var tasks = new List<Task>();
            foreach (var user in users)
            {
                var sel = selections[_rng.Next(selections.Length)];
                var stake = _rng.Next(5, 25) * 100L;
                var betId = $"b-{Interlocked.Increment(ref betSeq)}";
                var req = new PlaceBetRequest(betId, user, ev, mkt, sel, stake, quoteFor(sel), OddsToleranceAbs: 0.10m);

                if (duplicate && _rng.NextDouble() < 0.25)
                {
                    // Same bet id fired twice concurrently (a retrying client) — must place exactly once.
                    tasks.Add(Record(_engine.Betting.PlaceBetAsync(req, ct)));
                    tasks.Add(Record(_engine.Betting.PlaceBetAsync(req, ct)));
                }
                else
                {
                    tasks.Add(Record(_engine.Betting.PlaceBetAsync(req, ct)));
                }
            }
            await Task.WhenAll(tasks);

            async Task Record(Task<PlaceBetResult> t)
            {
                var r = await t;
                results.Add(r);
                if (r.Accepted) distinctPlaced.TryAdd(r.BetId, 0);
            }
        }

        decimal FreshQuote(string sel)
        {
            var m = _engine.Markets.GetAsync(ev, mkt, ct).GetAwaiter().GetResult();
            return m is not null && m.Odds.TryGetValue(sel, out var o) ? o : 2.0m;
        }

        // Partition bettors into four waves.
        var u = tally.Users;
        var a = u.Take(bettors * 40 / 100).ToList();
        var b = u.Skip(a.Count).Take(bettors * 20 / 100).ToList();
        var c = u.Skip(a.Count + b.Count).Take(bettors * 20 / 100).ToList();
        var d = u.Skip(a.Count + b.Count + c.Count).ToList();

        // A: fresh odds (accepted / insufficient), with duplicate retries for idempotency.
        await PlaceWave(a, FreshQuote, duplicate: true);

        // Market drifts hard — the snapshot quote is now stale beyond tolerance.
        await _engine.Markets.UpdateOddsAsync(ev, mkt, Drift(selections, 1.0m), ct);

        // B: clients still holding the opening quote → requoted (odds-changed).
        await PlaceWave(b, sel => staleQuote[sel], duplicate: false);

        // A goal is scored → suspend.
        await _engine.Markets.SuspendAsync(ev, mkt, ct);

        // C: bets arriving during suspension → refused.
        await PlaceWave(c, FreshQuote, duplicate: false);

        // Resume at repriced odds.
        await _engine.Markets.ResumeAsync(ev, mkt, Drift(selections, 0.5m), ct);

        // D: back open, fresh odds (accepted / insufficient), with duplicate retries.
        await PlaceWave(d, FreshQuote, duplicate: true);

        foreach (var r in results)
        {
            tally.Submissions++;
            if (r.WasReplay) tally.Replays++;
            if (r.Accepted) { tally.Accepted++; continue; }
            switch (r.Reason)
            {
                case RejectReason.OddsChanged: tally.RejStale++; break;
                case RejectReason.InsufficientFunds: tally.RejInsufficient++; break;
                case RejectReason.MarketSuspended: tally.RejSuspended++; break;
                default: tally.RejOther++; break;
            }
        }
        tally.DistinctPlaced = distinctPlaced.Count;
        return tally;
    }

    private Dictionary<string, decimal> Drift(string[] sels, decimal spread)
    {
        var map = new Dictionary<string, decimal>();
        foreach (var s in sels)
            map[s] = Math.Round(1.40m + (decimal)_rng.NextDouble() * spread * 3m, 2);
        return map;
    }
}
