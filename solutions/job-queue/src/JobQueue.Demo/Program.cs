using System.Collections.Concurrent;
using JobQueue.Infrastructure;
using JobQueue.Queue;

// ---------------------------------------------------------------------------------------------------
// A runnable demonstration of the reliable job queue: many producers enqueue a mix of immediate,
// delayed, prioritised, flaky and poison jobs; many competing workers drain them; some deliveries
// crash mid-work (forcing lease-expiry requeue); poison jobs always fail (forcing retry -> DLQ).
// At the end we prove: nothing lost, at-least-once, no double-complete, DLQ captured the poison.
// ---------------------------------------------------------------------------------------------------

Console.OutputEncoding = System.Text.Encoding.UTF8;
void Log(string m) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {m}");

Log("Connecting to embedded (InProc) Zaris store ...");
var client = await ZarisConnection.ConnectAsync("zaris://inproc/jobqueue-demo");
var store = new ZarisDocumentStore(client);

var options = new JobQueueOptions
{
    LeaseDuration = TimeSpan.FromMilliseconds(400), // short so crashed jobs requeue quickly in the demo
    DefaultMaxAttempts = 4,
    RetryBackoffBase = TimeSpan.FromMilliseconds(40),
    RetryBackoffCap = TimeSpan.FromSeconds(1),
    UseBackoffJitter = true,
    CompletedRetention = TimeSpan.FromMinutes(5),
};
var queue = new JobQueueClient(store, "emails", options, SystemClock.Instance);

// ---- the job population -------------------------------------------------------------------------
const int immediate = 40;
const int delayed = 10;
const int flaky = 10;   // fail a couple of times, then succeed (exercises retry+backoff, still completes)
const int poison = 8;   // always fail -> dead-letter
const int crashOnce = 12; // crash mid-work on first delivery -> lease-expiry requeue -> completes

var goodIds = new HashSet<string>();
var poisonIds = new HashSet<string>();
var crashIds = new HashSet<string>();
var flakyFailBudget = new ConcurrentDictionary<string, int>();

var toEnqueue = new List<(string Id, string Payload, int Priority, TimeSpan? Delay, int Max)>();

for (var i = 0; i < immediate; i++) { var id = $"email-{i}"; goodIds.Add(id); toEnqueue.Add((id, $"send welcome #{i}", i % 3, null, 4)); }
for (var i = 0; i < delayed; i++)   { var id = $"digest-{i}"; goodIds.Add(id); toEnqueue.Add((id, $"nightly digest #{i}", 1, TimeSpan.FromMilliseconds(300 + i * 40), 4)); }
for (var i = 0; i < flaky; i++)     { var id = $"flaky-{i}"; goodIds.Add(id); flakyFailBudget[id] = 2; toEnqueue.Add((id, $"charge card #{i}", 2, null, 4)); }
for (var i = 0; i < crashOnce; i++) { var id = $"crash-{i}"; goodIds.Add(id); crashIds.Add(id); toEnqueue.Add((id, $"resize image #{i}", 2, null, 4)); }
for (var i = 0; i < poison; i++)    { var id = $"poison-{i}"; poisonIds.Add(id); toEnqueue.Add((id, $"corrupt payload #{i}", 0, null, 2)); }

var totalJobs = toEnqueue.Count;

// ---- producers: 4 threads enqueue concurrently --------------------------------------------------
Log($"Enqueuing {totalJobs} jobs across 4 producers (good={goodIds.Count}, poison={poisonIds.Count}, " +
    $"of which crash-once={crashIds.Count}, flaky={flaky}, delayed={delayed}) ...");
var shuffled = toEnqueue.OrderBy(_ => Guid.NewGuid()).ToList();
await Task.WhenAll(Chunk(shuffled, 4).Select(chunk => Task.Run(async () =>
{
    foreach (var (id, payload, prio, delay, max) in chunk)
        await queue.EnqueueAsync(payload, priority: prio, delay: delay, maxAttempts: max, jobId: id);
})));
Log($"Enqueued. Initial depth: {await queue.StatsAsync()}");

// ---- handler: records runs; flaky fails a few times; poison always fails; crash dies mid-work ----
var runsPerJob = new ConcurrentDictionary<string, int>();
var completedOrder = new ConcurrentQueue<string>();
Func<Job, CancellationToken, Task> handler = async (job, ct) =>
{
    var runs = runsPerJob.AddOrUpdate(job.Id, 1, (_, n) => n + 1);
    await Task.Delay(Random.Shared.Next(2, 12), ct); // simulate real work

    if (poisonIds.Contains(job.Id))
        throw new InvalidOperationException("payload failed validation");

    if (crashIds.Contains(job.Id) && runs == 1)
        throw new WorkerCrashException("process killed mid-work");

    if (flakyFailBudget.TryGetValue(job.Id, out var budget) && budget > 0)
    {
        flakyFailBudget[job.Id] = budget - 1;
        throw new TimeoutException("downstream temporarily unavailable");
    }
    // success
};

// ---- 8 competing workers ------------------------------------------------------------------------
const int workerCount = 8;
var workers = Enumerable.Range(0, workerCount)
    .Select(i => new Worker($"worker-{i}", queue, handler, idleDelay: TimeSpan.FromMilliseconds(10)))
    .ToArray();

Log($"Starting {workerCount} competing workers ...");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var runTasks = workers.Select(wk => wk.RunAsync(cts.Token)).ToArray();

// ---- monitor until drained, printing live depth ------------------------------------------------
var sw = System.Diagnostics.Stopwatch.StartNew();
while (sw.Elapsed < TimeSpan.FromSeconds(55))
{
    var s = await queue.StatsAsync();
    var active = s.ReadyDue + s.ReadyScheduled + s.InFlight + s.LeaseExpired;
    Log($"  depth: {s}");
    if (active == 0 && s.DeadLettered == poison) break;
    await Task.Delay(300);
}
cts.Cancel();
try { await Task.WhenAll(runTasks); } catch (OperationCanceledException) { }

// ---- results / proof ----------------------------------------------------------------------------
Console.WriteLine();
Log("==================== RESULTS ====================");
var completedGood = 0;
var notCompleted = new List<string>();
foreach (var id in goodIds)
{
    var j = await queue.GetAsync(id);
    if (j?.State == JobState.Completed) completedGood++; else notCompleted.Add($"{id}:{j?.State}");
}
var dlq = (await queue.ListDeadLetterAsync()).OrderBy(x => x).ToList();
var activeLeft = await queue.ListActiveAsync();
var totalCompletedAuthoritative = workers.Sum(w => w.Stats.Completed);
var totalCrashed = workers.Sum(w => w.Stats.Crashed);
var totalRetries = workers.Sum(w => w.Stats.Failed);
var totalDead = workers.Sum(w => w.Stats.DeadLettered);
var totalRuns = runsPerJob.Values.Sum();

Log($"Good jobs completed      : {completedGood}/{goodIds.Count}" + (notCompleted.Count == 0 ? "  ✓" : $"  ✗ ({string.Join(",", notCompleted)})"));
Log($"Dead-letter queue        : {dlq.Count} jobs {(dlq.Count == poison ? "✓" : "✗")}  -> [{string.Join(", ", dlq)}]");
Log($"Active set drained        : {activeLeft.Count} remaining {(activeLeft.Count == 0 ? "✓" : "✗")}");
Log($"No loss (all terminal)   : {(completedGood == goodIds.Count && dlq.Count == poison ? "✓ every job accounted for" : "✗")}");
Log($"At-least-once deliveries : {totalRuns} handler runs for {totalJobs} jobs (>= {totalJobs}) {(totalRuns >= totalJobs ? "✓" : "✗")}");
Log($"No double-complete       : {totalCompletedAuthoritative} authoritative completions == {goodIds.Count} good jobs {(totalCompletedAuthoritative == goodIds.Count ? "✓" : "✗")}");
Log($"Crashes requeued         : {totalCrashed} crashes injected; crash-once jobs all completed via lease-expiry reclaim");
Log($"Retries (backoff)        : {totalRetries} transient failures re-scheduled; dead-lettered deliveries: {totalDead}");
Console.WriteLine();
Log("Per-worker tallies:");
foreach (var wk in workers)
    Log($"  {wk.Id}: claimed={wk.Stats.Claimed} completed={wk.Stats.Completed} retried={wk.Stats.Failed} " +
        $"dlq={wk.Stats.DeadLettered} crashed={wk.Stats.Crashed} lostLease={wk.Stats.LostLease}");

var ok = completedGood == goodIds.Count && dlq.Count == poison && activeLeft.Count == 0
         && totalCompletedAuthoritative == goodIds.Count && totalRuns >= totalJobs;
Console.WriteLine();
Log(ok ? "ALL GUARANTEES HELD ✓" : "GUARANTEE VIOLATION ✗");
Environment.Exit(ok ? 0 : 1);

static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int parts)
{
    var buckets = Enumerable.Range(0, parts).Select(_ => new List<T>()).ToArray();
    for (var i = 0; i < items.Count; i++) buckets[i % parts].Add(items[i]);
    return buckets;
}
