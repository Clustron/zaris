using System;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.Helpers;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.LeaderElection;

internal class LeaderElectionSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public LeaderElectionSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        var context = new SampleContext("election");
        var electionKey = context.Key("leader");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        var zaris = (IZaris)client;
        var leases = zaris.Leases;
        var watch = zaris.Watch;

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

        cts.Cancel();
        await Task.Delay(2000);

        ConsoleHelper.Success("Leader election demo completed.");

        // Cleanup
        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Sample cleanup completed.");
    }

    // ============================================================
    // NODE LOGIC
    // ============================================================

    private async Task RunNodeAsync(
        IZarisClient client,
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

                // Simulated crash
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