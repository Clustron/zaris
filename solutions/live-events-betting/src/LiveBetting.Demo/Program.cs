using System.Diagnostics;
using LiveBetting.Core;
using LiveBetting.Core.Domain;
using LiveBetting.Core.Services;
using LiveBetting.Demo;

// ─────────────────────────────────────────────────────────────────────────────
// Live in-play betting engine — end-to-end demonstration on an embedded Zaris store.
// Everything runs in-process; no external cluster, no ports.
// ─────────────────────────────────────────────────────────────────────────────

var ok = true;          // overall money-safety verdict
void Hdr(string s) { Console.WriteLine(); Console.WriteLine(new string('=', 78)); Console.WriteLine(s); Console.WriteLine(new string('=', 78)); }
void Check(string label, bool pass) { ok &= pass; Console.WriteLine($"   [{(pass ? "PASS" : "FAIL")}] {label}"); }
string M(long minor) => Money.Format(minor);

await using var engine = await BettingEngine.ConnectAsync("zaris://inproc/betting-demo");
Console.WriteLine("Connected to embedded Zaris store  zaris://inproc/betting-demo");

// ─────────────────────────────────────────────────────────────────────────────
// 1. Live odds feed: Zaris pub/sub (instant fan-out) + Zaris stream (durable replay)
// ─────────────────────────────────────────────────────────────────────────────
Hdr("1. LIVE ODDS FEED  (Zaris pub/sub + durable stream)");
{
    const string ev = "epl:ars-che", mkt = "winner";
    var sels = new[] { "home", "draw", "away" };
    await engine.Markets.CreateMarketAsync(ev, mkt, "Arsenal v Chelsea — Winner",
        new Dictionary<string, decimal> { ["home"] = 2.10m, ["draw"] = 3.40m, ["away"] = 3.60m });

    var received = 0;
    await using var sub = await engine.Markets.SubscribeAsync(e =>
    {
        var n = Interlocked.Increment(ref received);
        if (n <= 6)
            Console.WriteLine($"   live  » {e.Kind,-11} v{e.OddsVersion}  " +
                string.Join("  ", e.Odds.Select(kv => $"{kv.Key}={kv.Value:0.00}")) +
                (e.WinningSelection is { } w ? $"  winner={w}" : ""));
        return Task.CompletedTask;
    }, ev, mkt);

    // A burst of live odds changes + a suspend/resume (goal scored), as during a real match.
    for (var i = 0; i < 4; i++)
        await engine.Markets.UpdateOddsAsync(ev, mkt, new Dictionary<string, decimal>
            { ["home"] = 1.90m + i * 0.05m, ["draw"] = 3.30m, ["away"] = 3.80m - i * 0.05m });
    await engine.Markets.SuspendAsync(ev, mkt);
    await engine.Markets.ResumeAsync(ev, mkt, new Dictionary<string, decimal> { ["home"] = 1.55m, ["draw"] = 3.90m, ["away"] = 5.00m });

    await Task.Delay(100); // let the async fan-out drain
    var history = await engine.Markets.ReadFeedHistoryAsync(ev, mkt, 50);
    Console.WriteLine($"   pub/sub delivered {received} live events to the subscriber.");
    Console.WriteLine($"   durable stream holds {history.Count} replayable feed events (newest: {history[0].Kind}).");
    Check("live feed delivered over pub/sub and is durably replayable from the stream", received >= 6 && history.Count >= 6);
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. Atomic debit under extreme concurrency (the money-safety core)
// ─────────────────────────────────────────────────────────────────────────────
Hdr("2. RACE-FREE WALLET DEBIT  (1000 concurrent placements on ONE wallet)");
{
    const string ev = "atp:final", mkt = "winner";
    await engine.Markets.CreateMarketAsync(ev, mkt, "Tennis final",
        new Dictionary<string, decimal> { ["p1"] = 1.80m, ["p2"] = 2.10m });

    const string whale = "whale";
    const long opening = 1_000_00;  // 1000.00
    const long stake = 1_50;        // 1.50 per bet; demand (1500.00) far exceeds balance
    await engine.Wallets.EnsureWalletAsync(whale, opening);

    var sw = Stopwatch.StartNew();
    var results = await Task.WhenAll(Enumerable.Range(0, 1000).Select(i =>
        engine.Betting.PlaceBetAsync(new PlaceBetRequest($"whale-{i}", whale, ev, mkt, "p1", stake, 1.80m, OddsToleranceAbs: 1m))));
    sw.Stop();

    var accepted = results.Count(r => r.Accepted);
    var refused = results.Count(r => r.Reason == RejectReason.InsufficientFunds);
    var balance = await engine.Wallets.GetBalanceAsync(whale);

    Console.WriteLine($"   1000 concurrent bets in {sw.ElapsedMilliseconds} ms  →  accepted={accepted}  insufficient-funds={refused}");
    Console.WriteLine($"   wallet: opening {M(opening)}  final {M(balance)}  (each accepted bet staked {M(stake)})");
    Check("every accepted debit applied against a fresh balance (exact, no lost update)", balance == opening - accepted * stake);
    Check("wallet never went negative", balance >= 0);
    Check("accepted exactly as many bets as the balance could fund — not one more", accepted == (int)(opening / stake));
    Check("no bet both insufficient-and-accepted (counts reconcile)", accepted + refused == 1000);
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. Idempotent placement: a retried bet id never double-places or double-charges
// ─────────────────────────────────────────────────────────────────────────────
Hdr("3. IDEMPOTENT PLACEMENT  (same client bet id submitted many times)");
{
    const string ev = "nba:gsw-lal", mkt = "winner";
    await engine.Markets.CreateMarketAsync(ev, mkt, "GSW v LAL",
        new Dictionary<string, decimal> { ["gsw"] = 1.70m, ["lal"] = 2.20m });
    await engine.Wallets.EnsureWalletAsync("retry-user", 100_00);

    var req = new PlaceBetRequest("ticket-9", "retry-user", ev, mkt, "gsw", 40_00, 1.70m, OddsToleranceAbs: 1m);
    var r = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => engine.Betting.PlaceBetAsync(req)));

    var accepted = r.Count(x => x.Accepted);
    var replays = r.Count(x => x.WasReplay);
    var balance = await engine.Wallets.GetBalanceAsync("retry-user");
    Console.WriteLine($"   30 concurrent submissions of bet id 'ticket-9'  →  accepted-results={accepted}  flagged-as-replay={replays}");
    Console.WriteLine($"   wallet: 100.00  →  {M(balance)}   (stake 40.00 debited exactly once)");
    Check("stake debited exactly once despite 30 submissions", balance == 100_00 - 40_00);
    Check("all submissions reported the same (accepted) outcome", accepted == 30);
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. Stale-odds protection with a self-expiring requote token (write-time TTL)
// ─────────────────────────────────────────────────────────────────────────────
Hdr("4. ODDS-STALENESS PROTECTION  (requote + native write-time TTL)");
{
    const string ev = "ufc:main", mkt = "winner";
    await engine.Markets.CreateMarketAsync(ev, mkt, "UFC main event",
        new Dictionary<string, decimal> { ["red"] = 2.00m, ["blue"] = 1.80m });
    await engine.Wallets.EnsureWalletAsync("sharp", 100_00);

    // Bettor quotes 2.00, but odds drift to 2.60 before the bet lands.
    await engine.Markets.UpdateOddsAsync(ev, mkt, new Dictionary<string, decimal> { ["red"] = 2.60m });
    var r = await engine.Betting.PlaceBetAsync(new PlaceBetRequest("stale-1", "sharp", ev, mkt, "red", 20_00, 2.00m, OddsToleranceAbs: 0.10m));

    var token = await engine.Betting.GetRequoteAsync("stale-1");
    var ttl = await engine.Betting.GetRequoteTtlAsync("stale-1");
    Console.WriteLine($"   quoted 2.00, live 2.60, tolerance 0.10  →  {r.Status}/{r.Reason}, requote={r.RequoteOdds:0.00}");
    Console.WriteLine($"   requote token stored with write-time TTL — remaining {ttl?.TotalSeconds:0.0}s (self-reclaims, no delete).");
    Check("stale bet rejected and requoted, not struck on stale odds", r.Reason == RejectReason.OddsChanged && r.RequoteOdds == 2.60m);
    Check("no stake taken on a requote", await engine.Wallets.GetBalanceAsync("sharp") == 100_00);
    Check("requote token carries a live TTL set at write time", token is not null && ttl is { } t && t > TimeSpan.Zero && t <= BettingService.RequoteTtl);
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. Concurrent betting storm with injected hazards, then exactly-once settlement
// ─────────────────────────────────────────────────────────────────────────────
Hdr("5. BETTING STORM + SUSPENSION + EXACTLY-ONCE SETTLEMENT");
{
    const string ev = "wc:final", mkt = "winner";
    var sels = new[] { "home", "away" };
    await engine.Markets.CreateMarketAsync(ev, mkt, "World Cup final",
        new Dictionary<string, decimal> { ["home"] = 2.00m, ["away"] = 2.00m });

    var sim = new Simulator(engine);
    var sw = Stopwatch.StartNew();
    var tally = await sim.RunStormAsync(ev, mkt, sels, bettors: 400);
    sw.Stop();

    Console.WriteLine($"   {tally.Users.Count} bettors, {tally.Submissions} placement calls into a live, suspending market in {sw.ElapsedMilliseconds} ms:");
    Console.WriteLine($"      accepted results     : {tally.Accepted}  ({tally.DistinctPlaced} distinct bets placed; retries resolved to the same outcome)");
    Console.WriteLine($"      rejected (stale odds): {tally.RejStale}");
    Console.WriteLine($"      rejected (funds)     : {tally.RejInsufficient}");
    Console.WriteLine($"      rejected (suspended) : {tally.RejSuspended}");
    Console.WriteLine($"      duplicate retries    : {tally.Replays} (no second bet, no second debit)");

    // Money staked = money that left wallets during the storm.
    long walletsBefore = 0;
    foreach (var u in tally.Users) walletsBefore += await engine.Wallets.GetBalanceAsync(u);
    var staked = tally.OpeningFloatMinor - walletsBefore;

    // Settle the market: "home" wins. Winners are paid once; losers are closed.
    var winner = "home";
    var summary = await engine.Settlement.SettleMarketAsync(ev, mkt, winner);
    Console.WriteLine($"   SETTLE winner={winner}: settled={summary.Settled} won={summary.Won} lost={summary.Lost} " +
                      $"staked={M(summary.TotalStakeMinor)} paid={M(summary.TotalPayoutMinor)}");

    long walletsAfter = 0;
    foreach (var u in tally.Users) walletsAfter += await engine.Wallets.GetBalanceAsync(u);
    var anyNegative = false;
    foreach (var u in tally.Users) anyNegative |= await engine.Wallets.GetBalanceAsync(u) < 0;

    Console.WriteLine();
    Console.WriteLine("   MONEY-SAFETY AUDIT");
    Check("no wallet is negative", !anyNegative);
    Check("money staked during storm == total stake settled (no double-spend)", staked == summary.TotalStakeMinor);
    Check("conservation: opening float == final wallets + house net (stakes - payouts)",
        tally.OpeningFloatMinor == walletsAfter + (summary.TotalStakeMinor - summary.TotalPayoutMinor));

    // Exactly-once: re-run settlement 3x (simulating crash-recovery passes) and prove nothing moves.
    var before = walletsAfter;
    for (var i = 0; i < 3; i++) await engine.Settlement.SettleMarketAsync(ev, mkt, winner);
    long walletsReSettled = 0;
    foreach (var u in tally.Users) walletsReSettled += await engine.Wallets.GetBalanceAsync(u);
    Console.WriteLine();
    Console.WriteLine("   EXACTLY-ONCE SETTLEMENT");
    Check("re-running settlement 3x pays nobody twice (balances unchanged)", walletsReSettled == before);

    var log = await engine.Settlement.ReadSettlementLogAsync(ev, mkt, 100_000);
    Check("durable settlement log holds one record per settled bet", log.Count == summary.Settled);
    Console.WriteLine($"   durable settlement log: {log.Count} records (replayable from Zaris stream).");
}

Hdr(ok ? "RESULT: ALL MONEY-SAFETY & EXACTLY-ONCE INVARIANTS HELD  ✅"
       : "RESULT: AN INVARIANT FAILED  ❌");
return ok ? 0 : 1;
