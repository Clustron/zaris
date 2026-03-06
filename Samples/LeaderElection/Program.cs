using System;
using System.Threading;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.DKV.Client.Helpers;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.LeaderElection
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Leader Election via Lease");

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()
                ?? throw new InvalidOperationException("Missing Dkv configuration.");

            var mode = options.GetMode();
            IDkvClient client;
            // Initialize client (EXPLICIT API USAGE)
            if (mode == DkvClientMode.Remote)
            {
                if (options.Seeds == null || options.Seeds.Count == 0)
                    throw new InvalidOperationException("No seed servers configured.");

                client = await DKVClient.InitializeRemote(
                    options.ClusterId,
                    options.Seeds,
                    options.LogFilePath);
            }
            else
            {
                client = await DKVClient.InitializeInProc(
                    options.ClusterId,
                    options.LogFilePath);
            }

            ConsoleHelper.Success("Connected to cluster.");
            SampleEnvironmentPrinter.Print(options, mode);

            var context = new SampleContext("election");
            var electionKey = context.Key("leader");

            // Start clean
            await client.ClearAsync(new ClearRequest(context.Prefix));

            var leases = ((IDkv)client).Leases;
            var watch = ((IDkv)client).Watch;

            ConsoleHelper.Info($"Election Key: {electionKey}");

            var cts = new CancellationTokenSource();

            // Simulate 3 nodes
            for (int i = 1; i <= 3; i++)
            {
                int nodeId = i;
                _ = Task.Run(() =>
                    RunNodeAsync(client, leases, watch, electionKey, nodeId, cts.Token));
            }

            ConsoleHelper.Info("Running for 40 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(40));

            // Stop nodes
            cts.Cancel();

            await Task.Delay(2000); // small grace period

            ConsoleHelper.Success("Leader election demo completed.");

            // CLEANUP (same pattern as other samples)
            ConsoleHelper.Section("Cleanup");
            await client.ClearAsync(new ClearRequest(context.Prefix));
            ConsoleHelper.Success("Sample cleanup completed.");

            Console.WriteLine("\nDone.");
        }

        static async Task RunNodeAsync(
            IDkvClient client,
            ILeasesClient leases,
            IWatchClient watch,
            string electionKey,
            int nodeId,
            CancellationToken token)
        {
            string nodeName = $"node-{nodeId}";
            var random = new Random(nodeId * Environment.TickCount);

            const int LeaseTtlSeconds = 8;
            const int CrashAfterSeconds = 4;
            const int KeepAliveIntervalSeconds = 2;

            while (!token.IsCancellationRequested)
            {
                Console.WriteLine($"{nodeName} attempting election...");

                await Task.Delay(random.Next(300, 1000), token);

                var lease = await leases.GrantAsync(TimeSpan.FromSeconds(LeaseTtlSeconds));
                if (!lease.IsSuccess)
                {
                    await Task.Delay(1000, token);
                    continue;
                }

                var leaseId = lease.Value;

                var put = await client.PutAsync(
                    electionKey,
                    nodeName,
                    Put.IfAbsent().WithLease(leaseId));

                if (put.IsSuccess)
                {
                    ConsoleHelper.Success($"{nodeName} became LEADER.");

                    var leaderCts = new CancellationTokenSource();

                    // Simulate crash
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(CrashAfterSeconds));
                        ConsoleHelper.Error($"{nodeName} CRASHED.");
                        leaderCts.Cancel();
                    });

                    try
                    {
                        while (!leaderCts.Token.IsCancellationRequested &&
                               !token.IsCancellationRequested)
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(KeepAliveIntervalSeconds),
                                leaderCts.Token);

                            await leases.KeepAliveAsync(leaseId);
                        }
                    }
                    catch (TaskCanceledException) { }

                    return;
                }
                else
                {
                    await leases.RevokeAsync(leaseId);

                    var tcs = new TaskCompletionSource();

                    var watchResult = await watch.WatchKeyAsync(
                        electionKey,
                        null,
                        ev =>
                        {
                            if (ev.EventType == WatchEventType.Delete)
                                tcs.TrySetResult();
                        });

                    await tcs.Task;

                    Console.WriteLine($"{nodeName} detected leader loss.");
                    await watchResult.Subscription.StopAsync();
                }
            }
        }
    }
}