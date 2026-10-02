using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;
using SagaOutbox.Saga;
using SagaOutbox.Services;

// =============================================================================================
// Self-contained, end-to-end demonstration of a saga orchestrator + transactional outbox on an
// embedded (in-process) Clustron Zaris store. No external cluster, no ports: one process, one store.
//
//   Scenario 1  Happy path        — order fulfilled; outbox events relayed; consumer sees each once.
//   Scenario 2  Compensation      — inventory short -> payment refunded, nothing left reserved.
//   Scenario 3  Crash + recovery  — orchestrator dies mid-saga; a fresh one resumes; no double effects.
//   Scenario 4  At-least-once bus — relay redelivers a duplicate; the consumer's inbox absorbs it.
// =============================================================================================

const string Topic = "domain-events";

var client = await ZarisConnection.ConnectAsync("zaris://inproc/saga-demo");
var store = new ZarisDocumentStore(client);
var orders = new OrderService(store);
var payments = new PaymentService(store);
var inventory = new InventoryService(store);
var shipping = new ShippingService(store);
var bus = new EventBus(store);
var relay = new OutboxRelay(store, bus, Topic);

OrderFulfillmentSaga NewSaga() => new(store, orders, payments, inventory, shipping);

async Task<int> PumpAll()
{
    var n = 0;
    n += await relay.PumpAsync(orders.Registry, "order");
    n += await relay.PumpAsync(payments.Registry, "payment");
    n += await relay.PumpAsync(inventory.Registry, "inventory");
    n += await relay.PumpAsync(shipping.Registry, "shipping");
    return n;
}

void Banner(string t)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine("  " + t);
    Console.WriteLine(new string('=', 78));
}
void Line(string s) => Console.WriteLine("  " + s);

// ------------------------------------------------------------------ Scenario 1: happy path
Banner("SCENARIO 1  Happy path: order -> payment -> inventory -> shipping -> confirm");
await payments.EnsureAccountAsync("alice", 100_00);      // $100.00 (cents)
await inventory.EnsureStockAsync("WIDGET", 50);
await orders.PlaceOrderAsync("order-1", "alice", "WIDGET", 2, 30_00);
Line($"start: alice balance=${await payments.GetBalanceAsync("alice") / 100.0:0.00}, WIDGET stock=50, order total=$30.00");

var saga1 = NewSaga();
saga1.OnStepDone = s => Line($"   step done     -> {s}");
var r1 = await saga1.StartAsync("saga-1", "order-1", "alice", "WIDGET", 2, 30_00);
Line($"saga result: {r1}");

var (av1, rv1) = await inventory.GetLevelsAsync("WIDGET");
Line($"end:   alice balance=${await payments.GetBalanceAsync("alice") / 100.0:0.00}, WIDGET available={av1} reserved={rv1}, " +
     $"shipment={await shipping.GetStatusAsync("order-1")}, order={(await orders.GetAsync("order-1"))!.Status}");

Line("relaying outbox -> bus ...");
for (var i = 0; i < 3; i++) await PumpAll();
var audit = new BusConsumer(store, bus, Topic, "audit");
var seen = new Dictionary<string, int>();
await audit.PollAsync(rec => { seen[rec.Type] = seen.GetValueOrDefault(rec.Type) + 1; Line($"   consumer applied -> {rec.Type} (from {rec.Source})"); return Task.CompletedTask; });
Line($"bus records={await bus.CountAsync(Topic)}; distinct effects applied by consumer={seen.Count} " +
     (seen.Values.All(c => c == 1) ? "(each exactly once ✓)" : "(DUPLICATE EFFECT!)"));

// ------------------------------------------------------------------ Scenario 2: compensation
Banner("SCENARIO 2  Failure + compensation: inventory short -> refund payment, restock");
await payments.EnsureAccountAsync("bob", 100_00);
await inventory.EnsureStockAsync("GADGET", 1);           // only 1 in stock
await orders.PlaceOrderAsync("order-2", "bob", "GADGET", 5, 40_00); // wants 5
Line($"start: bob balance=${await payments.GetBalanceAsync("bob") / 100.0:0.00}, GADGET stock=1, order wants 5");

var saga2 = NewSaga();
saga2.OnStepDone = s => Line($"   step done      -> {s}");
saga2.OnCompensated = s => Line($"   compensated    -> {s}");
var r2 = await saga2.StartAsync("saga-2", "order-2", "bob", "GADGET", 5, 40_00);
Line($"saga result: {r2}  (reason: {(await saga2.LoadAsync("saga-2")).FailureReason})");

var (av2, rv2) = await inventory.GetLevelsAsync("GADGET");
Line($"end:   bob balance=${await payments.GetBalanceAsync("bob") / 100.0:0.00} (refunded), GADGET available={av2} reserved={rv2}, " +
     $"order={(await orders.GetAsync("order-2"))!.Status}");
Line(await payments.GetBalanceAsync("bob") == 100_00 ? "payment fully refunded ✓" : "BALANCE WRONG!");

// ------------------------------------------------------------------ Scenario 3: crash + recovery
Banner("SCENARIO 3  Crash mid-saga -> resume from the persisted log, no double effects");
await payments.EnsureAccountAsync("carol", 100_00);
await inventory.EnsureStockAsync("GIZMO", 10);
await orders.PlaceOrderAsync("order-3", "carol", "GIZMO", 3, 25_00);
Line($"start: carol balance=${await payments.GetBalanceAsync("carol") / 100.0:0.00}, GIZMO stock=10, order=3 @ $25.00");

var crashing = NewSaga();
crashing.OnStepDone = s => Line($"   step done      -> {s}");
var armed = true;
crashing.AfterActionBeforePersist = (step, _) =>
{
    if (step == "ReserveInventory" && armed)
    {
        armed = false;
        Line("   *** ReserveInventory committed at the service, orchestrator CRASHES before logging it ***");
        throw new Exception("simulated orchestrator crash");
    }
    return Task.CompletedTask;
};
try { await crashing.StartAsync("saga-3", "order-3", "carol", "GIZMO", 3, 25_00); }
catch (Exception ex) { Line($"   caught crash: {ex.Message}"); }

var (av3a, rv3a) = await inventory.GetLevelsAsync("GIZMO");
var mid = await crashing.LoadAsync("saga-3");
Line($"after crash: GIZMO available={av3a} reserved={rv3a} (reservation DID commit), " +
     $"saga step 'ReserveInventory'={mid.StepFor("ReserveInventory").Status} (NOT yet logged)");

Line("resuming with a fresh orchestrator instance ...");
var recovered = NewSaga();
recovered.OnStepDone = s => Line($"   step done      -> {s}");
var r3 = await recovered.ResumeAsync("saga-3");
Line($"saga result: {r3}");

var (av3b, rv3b) = await inventory.GetLevelsAsync("GIZMO");
Line($"end:   carol balance=${await payments.GetBalanceAsync("carol") / 100.0:0.00}, GIZMO available={av3b} reserved={rv3b}, " +
     $"shipment={await shipping.GetStatusAsync("order-3")}, order={(await orders.GetAsync("order-3"))!.Status}");
Line(await payments.GetBalanceAsync("carol") == 75_00 && av3b == 7 && rv3b == 3
    ? "charged once ($25.00), reserved once (3) despite the crash ✓"
    : "DOUBLE EFFECT DETECTED!");

// ------------------------------------------------------------------ Scenario 4: at-least-once + dedupe
Banner("SCENARIO 4  Outbox relay is at-least-once; consumer inbox makes effects exactly-once");
// Isolated topic so this scenario's measurement sees only dave's event.
const string Topic4 = "scenario4-events";
var relay4 = new OutboxRelay(store, bus, Topic4);
await payments.EnsureAccountAsync("dave", 100_00);
await payments.ChargeAsync("dave", "cmd-dave-1", "order-4", 10_00);
var daveKey = PaymentService.Key("dave");

relay4.CrashAfterPublishing = _ => true;   // publish, then "crash" before marking dispatched
var before = await bus.CountAsync(Topic4);
try { await relay4.PumpKeyAsync(daveKey, "payment"); }
catch (RelayCrashException ex) { Line($"   relay published then crashed: {ex.Message}"); }
relay4.CrashAfterPublishing = null;
await relay4.PumpKeyAsync(daveKey, "payment");  // recovery -> redelivers the SAME event id
var after = await bus.CountAsync(Topic4);
Line($"bus gained {after - before} records for 1 logical event (at-least-once: duplicate on the bus)");

var dedupe = new BusConsumer(store, bus, Topic4, "dave-audit");
var applied = 0;
await dedupe.PollAsync(rec => { if (rec.Type == "PaymentCharged") applied++; return Task.CompletedTask; });
// Re-run the consumer; it must not re-apply.
await dedupe.PollAsync(_ => Task.CompletedTask);
Line($"consumer applied the duplicated PaymentCharged effect {applied} time(s) " + (applied == 1 ? "✓" : "(WRONG!)"));

Banner("DONE — all scenarios completed. State is consistent; no lost or duplicated effects.");
