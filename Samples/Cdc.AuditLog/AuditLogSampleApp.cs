using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions.ChangeFeed;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.ChangeFeed;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.Configuration;

namespace Clustron.Zaris.Sample.Cdc.AuditLog;

/// <summary>
/// The canonical Change Data Capture (CDC) sample: a durable consumer that logs EVERY mutation
/// (Put / Delete) happening in the store, like an audit / change log — and proves that, after a
/// restart, it resumes from where it left off instead of replaying everything again.
/// </summary>
internal sealed class AuditLogSampleApp
{
    private readonly IZarisClientProvider _provider;
    private readonly IConfiguration _config;

    // The store name matches the "teststore" connection string in appsettings.json.
    private const string StoreName = "teststore";

    // Everything the demo writes lives under this prefix, so the feed can watch exactly it.
    private const string Prefix = "audit/";

    // A FIXED, durable consumer name is what makes resume-on-restart work: the server remembers
    // this consumer's committed offset, so the next run continues past what it already processed.
    private const string DurableConsumer = "audit-logger";

    // Each run writes this many mutations (5 puts + 1 delete).
    private const int EventsPerRun = 6;

    public AuditLogSampleApp(IZarisClientProvider provider, IConfiguration config)
    {
        _provider = provider;
        _config = config;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);
        ConsoleHelper.Success($"Connected to store: {StoreName}");

        // The change feed needs the server op-log and the remote client; the embedded (InProc)
        // engine does not implement it. This is the ONE way the CDC samples differ from the others
        // (which default to InProc): they default to a Remote connection string — see the README.
        if (client is not IZarisChangeFeedClient feed)
        {
            ConsoleHelper.Error(
                "This sample needs a running cluster with the change feed enabled (ZARIS_WAN_SYNC_FEED=1). " +
                "Point the 'teststore' connection string in appsettings.json at your cluster " +
                "(zaris://host:port/teststore) — the in-process engine cannot serve a change feed.");
            return;
        }

        // `--fresh true` uses a brand-new consumer name, which replays the whole retained feed from
        // the start — the contrast that makes the durable consumer's resume visible.
        bool fresh = _config.GetValue<bool>("fresh");
        string consumer = fresh ? $"audit-adhoc-{Guid.NewGuid():N}" : DurableConsumer;

        // A per-run id stamped into every key this run writes, so we can tell our own changes apart
        // from changes written by EARLIER runs when the feed delivers them.
        long runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ConsoleHelper.Info($"Consumer : {consumer}  " +
            (fresh ? "(fresh — replays from the start)" : "(durable — resumes from its last committed offset)"));
        ConsoleHelper.Info($"Prefix   : {Prefix}");
        ConsoleHelper.Info($"Run id   : {runId}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // ---- 1) produce this run's changes --------------------------------------------------------
        ConsoleHelper.Section("Writing this run's changes");
        for (int n = 0; n < 5; n++)
            await client.PutAsync($"{Prefix}{runId}/item-{n}", $"value-{n}");
        await client.DeleteAsync($"{Prefix}{runId}/item-4");
        ConsoleHelper.Info($"Wrote {EventsPerRun} mutations (5 puts + 1 delete). " +
            "They are already durable in the op-log, so the feed will deliver them even though we " +
            "subscribe AFTER writing (unlike a live Watch).");

        // ---- 2) consume + log the change feed -----------------------------------------------------
        ConsoleHelper.Section("Audit log (every mutation on the change feed)");

        var seen = new HashSet<string>();     // dedup: at-least-once delivery can re-deliver an event
        int total = 0, mine = 0, earlier = 0;
        long firstRevision = -1;

        var options = new ChangeFeedOptions
        {
            KeyPrefix    = Prefix,                                         // server-side filter
            ConsumerName = consumer,                                       // durable → resumable offset
            Kinds        = new[] { FeedEventKind.Put, FeedEventKind.Delete },
        };

        var consume = Task.Run(async () =>
        {
            try
            {
                await foreach (var ev in feed.OpenChangeFeed(options, cts.Token).ConfigureAwait(false))
                {
                    // Dedup by key+kind+revision so a re-delivery is counted once (idempotent apply).
                    if (!seen.Add($"{ev.Key}|{ev.Kind}|{ev.Revision}")) continue;

                    total++;
                    if (firstRevision < 0) firstRevision = ev.Revision;

                    bool isMine = TryGetRunId(ev.Key, out long evRun) && evRun == runId;
                    if (isMine) mine++; else earlier++;

                    string ts = DateTimeOffset.FromUnixTimeMilliseconds(ev.WriteUnixMs).ToString("HH:mm:ss.fff");
                    Console.WriteLine($"[AUDIT] {ts}  {ev.Kind,-6}  {ev.Key}  rev={ev.Revision}  " +
                        (isMine ? "(this run)" : "(earlier run)"));

                    // Once we have logged all of this run's changes, stop after a short grace.
                    if (mine >= EventsPerRun) cts.CancelAfter(TimeSpan.FromMilliseconds(750));
                }
            }
            catch (OperationCanceledException) { /* stopped: grace elapsed or Ctrl+C */ }
        }, cts.Token);

        // Stop when this run's changes are logged (+grace), on Ctrl+C, or after a safety timeout.
        await Task.WhenAny(consume, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (!cts.IsCancellationRequested) cts.Cancel();
        try { await consume.ConfigureAwait(false); } catch { }

        // ---- 3) summary: resume vs replay, made explicit ------------------------------------------
        ConsoleHelper.Section("Summary");
        Console.WriteLine($"Events logged this session : {total}");
        Console.WriteLine($"  from THIS run            : {mine}");
        Console.WriteLine($"  from EARLIER runs        : {earlier}");
        Console.WriteLine($"First revision this session: {firstRevision}");

        if (earlier == 0)
            ConsoleHelper.Success(
                $"RESUMED: the durable consumer '{consumer}' continued from its last committed offset — " +
                "it did NOT re-read changes it had already processed. Run this sample again (same " +
                "consumer) and it will skip everything above and log only the next run's changes.");
        else
            ConsoleHelper.Success(
                $"REPLAYED: consumer '{consumer}' re-read {earlier} change(s) from earlier runs, from the " +
                "start of the retained feed — which is what a fresh consumer name does (`--fresh true`).");
    }

    // Keys written by this sample look like 'audit/<runId>/item-<n>'. Pull out <runId>.
    private static bool TryGetRunId(string key, out long runId)
    {
        runId = 0;
        string[] parts = key.Split('/');
        return parts.Length >= 2 && long.TryParse(parts[1], out runId);
    }
}
