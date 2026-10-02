using System.Diagnostics;
using FeatureFlags.Admin;
using FeatureFlags.Client;
using FeatureFlags.Infrastructure;
using FeatureFlags.Model;

// ---------------------------------------------------------------------------------------------------
// Feature-Flags on Zaris — live demonstration.
//
// Everything below runs against ONE embedded (in-process) Zaris store: one admin connection and
// several SDK-client connections, all sharing the same backing engine. No external cluster, no ports.
// Swap the connection string for zaris://host:port/<store> to run the exact same code on a cluster.
// ---------------------------------------------------------------------------------------------------

const string store = "zaris://inproc/feature-flags-demo";

static void Banner(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 92));
    Console.WriteLine("  " + title);
    Console.WriteLine(new string('=', 92));
}

var adminConn = await ZarisConnection.ConnectAsync(store);
var admin = new FeatureFlagAdmin(new ZarisDocumentStore(adminConn));

// Three independent "app nodes", each with its own client + connection, polling every 250 ms.
async Task<FeatureFlagClient> NewNode(string name)
{
    var conn = await ZarisConnection.ConnectAsync(store);
    var c = new FeatureFlagClient(new ZarisDocumentStore(conn),
        new FeatureFlagClientOptions { Name = name, RefreshInterval = TimeSpan.FromMilliseconds(250) });
    await c.StartAsync();
    return c;
}

var node1 = await NewNode("node-1");
var node2 = await NewNode("node-2");
var node3 = await NewNode("node-3");
var nodes = new[] { node1, node2, node3 };

// ===================================================================================================
Banner("1.  Define flags (versioned ruleset document, written via CAS)");
// ===================================================================================================

// A boolean flag, currently OFF for everyone.
await admin.DefineFlagAsync(FlagDefinition.Boolean("new-checkout", enabled: false, fallthrough: true), "release-bot");

// A multivariate string flag with targeting rules.
var banner = FlagDefinition.Multivariate("homepage-banner", "control", "holiday", "blackfriday");
banner.Rules.Add(new TargetingRule(1, new Clause("country", ClauseOp.In, "US", "CA"))); // NA => holiday
banner.Rules.Add(new TargetingRule(2, new Clause("plan", ClauseOp.In, "enterprise"))); // enterprise => blackfriday
await admin.DefineFlagAsync(banner, "growth-team");

// A numeric flag for a tunable limit.
await admin.DefineFlagAsync(FlagDefinition.Numeric("upload-limit-mb", 25, 100, 500), "ops");

var rs = await admin.GetRulesetAsync();
Console.WriteLine($"Ruleset v{rs.Version} written with {rs.Flags.Count} flags: {string.Join(", ", rs.Flags.Keys)}");
foreach (var n in nodes) await n.RefreshAsync();
Console.WriteLine($"All 3 app nodes are now at ruleset v{node1.Version}.");

// ===================================================================================================
Banner("2.  Targeting rules + multivariate evaluation (local, no network per check)");
// ===================================================================================================

var alice = UserContext.For("alice").With("country", "US").With("plan", "pro").Build();
var bjorn = UserContext.For("bjorn").With("country", "SE").With("plan", "free").Build();
var corp  = UserContext.For("corp").With("country", "DE").With("plan", "enterprise").Build();

foreach (var (label, u) in new[] { ("alice (US/pro)", alice), ("bjorn (SE/free)", bjorn), ("corp (DE/enterprise)", corp) })
{
    var d = node1.Evaluate("homepage-banner", u);
    Console.WriteLine($"  homepage-banner for {label,-22} => \"{d.Value}\"   (reason: {d.Reason})");
}
Console.WriteLine($"  upload-limit-mb for alice            => {node1.NumberVariation("upload-limit-mb", alice)} MB");

// ===================================================================================================
Banner("3.  LIVE FLIP — admin flips a flag; measure how long until all nodes see it (no restart)");
// ===================================================================================================

var user42 = new UserContext("user-42");
Console.WriteLine($"  Before flip: " + string.Join(", ", nodes.Select(n => $"{n.Name}={n.BoolVariation("new-checkout", user42)}")));

var sw = Stopwatch.StartNew();
var newVersion = await admin.SetEnabledAsync("new-checkout", true, "release-bot");
Console.WriteLine($"  >>> admin flipped new-checkout ON (ruleset is now v{newVersion}); nodes poll every 250 ms...");

while (!nodes.All(n => n.BoolVariation("new-checkout", user42)))
    await Task.Delay(10);
sw.Stop();

Console.WriteLine($"  After flip:  " + string.Join(", ", nodes.Select(n => $"{n.Name}={n.BoolVariation("new-checkout", user42)}")));
Console.WriteLine($"  >>> propagated to ALL 3 nodes in ~{sw.ElapsedMilliseconds} ms (bounded by the 250 ms refresh window).");

// ===================================================================================================
Banner("4.  PERCENTAGE ROLLOUT — deterministic, sticky, consistent-hash bucketing");
// ===================================================================================================

const int population = 50_000;
var users = Enumerable.Range(0, population).Select(i => new UserContext($"user-{i}")).ToArray();

// Roll "new-checkout" out to 20%.
await admin.SetBooleanRolloutAsync("new-checkout", 20, "pm");
while (node1.Version < newVersion + 1) await Task.Delay(10);
foreach (var n in nodes) await n.RefreshAsync();

var at20 = users.Where(u => node1.BoolVariation("new-checkout", u)).Select(u => u.Key).ToHashSet();
Console.WriteLine($"  20% rollout over {population:N0} users:");
Console.WriteLine($"     node-1 admitted {at20.Count:N0}  = {100.0 * at20.Count / population:0.00}%");

// Consistency: every node agrees on every user's assignment (same deterministic hash everywhere).
var disagreements = users.Count(u => node1.BoolVariation("new-checkout", u) != node3.BoolVariation("new-checkout", u));
Console.WriteLine($"     node-1 vs node-3 disagreements: {disagreements}  (deterministic => identical on every node)");

// Widen to 50% and show stickiness: nobody who was in at 20% drops out.
await admin.SetBooleanRolloutAsync("new-checkout", 50, "pm");
await Task.Delay(300);
foreach (var n in nodes) await n.RefreshAsync();

var at50 = users.Where(u => node1.BoolVariation("new-checkout", u)).Select(u => u.Key).ToHashSet();
var droppedOut = at20.Except(at50).Count();
Console.WriteLine($"  Widened 20% -> 50%:");
Console.WriteLine($"     now admitted {at50.Count:N0} = {100.0 * at50.Count / population:0.00}%");
Console.WriteLine($"     users who were in at 20% but dropped out at 50%: {droppedOut}  (sticky => expected 0)");

// ===================================================================================================
Banner("5.  CONCURRENT ADMIN EDITS — many admins editing at once, zero lost updates (CAS)");
// ===================================================================================================

for (var i = 0; i < 25; i++)
    await admin.DefineFlagAsync(FlagDefinition.Boolean($"exp-{i}", enabled: false), "seed");

// 25 separate admin connections concurrently enable 25 distinct flags.
async Task<FeatureFlagAdmin> NewAdmin()
    => new(new ZarisDocumentStore(await ZarisConnection.ConnectAsync(store)));

var admins = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => NewAdmin()));
var before = (await admin.GetRulesetAsync()).Version;
await Task.WhenAll(Enumerable.Range(0, 25).Select(i => admins[i].SetEnabledAsync($"exp-{i}", true, $"admin-{i}")));

var finalRs = await admin.GetRulesetAsync();
var enabledCount = Enumerable.Range(0, 25).Count(i => finalRs.Flags[$"exp-{i}"].Enabled);
Console.WriteLine($"  25 admins concurrently enabled 25 flags.");
Console.WriteLine($"     flags actually enabled: {enabledCount}/25   (lost updates: {25 - enabledCount})");
Console.WriteLine($"     ruleset version advanced by exactly {finalRs.Version - before} (one commit per edit).");

// ===================================================================================================
Banner("6.  AUDIT TRAIL — every change recorded (who / when / old -> new), appended via CAS");
// ===================================================================================================

var audit = await admin.GetAuditAsync();
Console.WriteLine($"  {audit.Count} total audit entries. Most recent 6:");
foreach (var e in audit.TakeLast(6))
    Console.WriteLine($"     #{e.Seq,-3} v{e.RulesetVersion,-3} {e.AtUtc:HH:mm:ss} {e.Actor,-10} {e.Action,-14} {e.FlagKey,-14} {e.Old ?? "-"} -> {e.New ?? "-"}");

Console.WriteLine();
Console.WriteLine("Done. Shutting down nodes.");
foreach (var n in nodes) await n.DisposeAsync();
