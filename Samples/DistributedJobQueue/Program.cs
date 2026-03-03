using System;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.DKV.Client.Helpers;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.SimpleEnterpriseQueue
{
    internal class Program
    {
        private const string Entity = "job";
        private const int TotalJobs = 10;

        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV - Simplified Enterprise Job Queue");

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()!;

            IDkvClient client =
                options.GetMode() == DkvClientMode.Remote
                ? await DKVClient.InitializeRemote(options.ClusterId, options.Seeds!, options.LogFilePath)
                : await DKVClient.InitializeInProc(options.ClusterId, options.LogFilePath);

            ConsoleHelper.Success("Connected.");

            var context = new SampleContext("simple-queue");

            await ProduceJobs(client, context);

            ConsoleHelper.Section("Starting Workers");

            await Task.WhenAll(
                RunWorkerAsync(client, context, "worker-1"),
                RunWorkerAsync(client, context, "worker-2"),
                RunWorkerAsync(client, context, "worker-3")
            );

            ConsoleHelper.Success("\nQueue processing completed.");

            // Print final authoritative count
            var completedCount = await CountByStatusAsync((IDkv)client, "completed");

            Console.WriteLine();
            Console.WriteLine($"Total Completed Jobs: {completedCount}");

            // Cleanup (like Basic sample)
            ConsoleHelper.Section("Cleanup");
            await client.ClearAsync(new ClearRequest(context.Prefix));
            ConsoleHelper.Success("Sample cleanup completed.");

            Console.WriteLine("\nDone.");
        }

        // ============================================================
        // PRODUCER
        // ============================================================

        private static async Task ProduceJobs(IDkvClient client, SampleContext context)
        {
            ConsoleHelper.Section("Producing 10 Jobs");

            for (int i = 1; i <= TotalJobs; i++)
            {
                var job = new JobItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = i
                };

                var key = context.Key($"job:{job.Id}");

                await client.PutAsync(
                    key,
                    job,
                    Put.WithEntity(Entity)
                       .WithLabel("status", "pending"));

                Console.WriteLine($"Produced: invoice #{i}");
            }
        }

        // ============================================================
        // WORKER
        // ============================================================

        private static async Task RunWorkerAsync(
            IDkvClient client,
            SampleContext context,
            string workerName)
        {
            var dkv = (IDkv)client;

            var leaseResult =
                await dkv.Leases.GrantAsync(TimeSpan.FromSeconds(5));

            if (!leaseResult.IsSuccess)
                return;

            var lease = leaseResult.Value;

            while (true)
            {
                // Terminate based on authoritative completed state
                var completedCount = await CountByStatusAsync(dkv, "completed");
                if (completedCount >= TotalJobs)
                {
                    Console.WriteLine($"{workerName} exiting - all jobs completed.");
                    return;
                }

                // Find pending job
                var query = SearchQuery
                    .For(Entity)
                    .Eq("status", "pending")
                    .Limit(1);

                await using var reader =
                    await (await dkv.Scan.SearchAsync(query)).AsEntries();

                if (await reader.ReadAsync())
                {
                    var key = reader.Current.Key;

                    var get = await client.GetAsync<JobItem>(key);
                    if (!get.IsSuccess || !get.Version.HasValue)
                        continue;

                    if (!get.Metadata.Labels.TryGetValue("status", out var status) ||
                        status.Value != "pending")
                        continue;

                    var cas = await client.PutAsync(
                        key,
                        get.Value!,
                        Put.WithIfMatch(get.Version.Value)
                           .WithEntity(Entity)
                           .WithLabel("status", "processing"));

                    if (!cas.IsSuccess)
                        continue;

                    var lockKey = context.Key($"job:lock:{get.Value!.Id}");

                    var existingLock = await client.GetAsync<string>(lockKey);
                    if (existingLock.IsSuccess)
                    {
                        await client.PutAsync(
                            key,
                            get.Value!,
                            Put.WithIfMatch(cas.Version!.Value)
                               .WithEntity(Entity)
                               .WithLabel("status", "pending"));
                        continue;
                    }

                    await client.PutAsync(
                        lockKey,
                        "lock",
                        Put.WithLease(lease));

                    Console.WriteLine($"{workerName} processing invoice #{get.Value.InvoiceNumber}");

                    await Task.Delay(1000);

                    var invoice = get.Value.InvoiceNumber;

                    // Deterministic failure handling
                    if (Random.Shared.Next(0, 6) == 0)
                    {
                        Console.WriteLine($"{workerName} FAILED invoice #{invoice} (simulated).");

                        await client.PutAsync(
                            key,
                            get.Value!,
                            Put.WithIfMatch(cas.Version!.Value)
                               .WithEntity(Entity)
                               .WithLabel("status", "pending"));

                        await client.DeleteAsync(lockKey);
                        continue;
                    }

                    // Mark completed (authoritative state)
                    var completeCas = await client.PutAsync(
                        key,
                        get.Value!,
                        Put.WithIfMatch(cas.Version!.Value)
                           .WithEntity(Entity)
                           .WithLabel("status", "completed"));

                    if (!completeCas.IsSuccess)
                        continue;

                    await client.DeleteAsync(lockKey);

                    Console.WriteLine($"{workerName} completed invoice #{invoice}.");
                }
                else
                {
                    // Recovery
                    var recoveryQuery = SearchQuery
                        .For(Entity)
                        .Eq("status", "processing")
                        .Limit(1);

                    await using var recoveryReader =
                        await (await dkv.Scan.SearchAsync(recoveryQuery)).AsEntries();

                    if (await recoveryReader.ReadAsync())
                    {
                        var key2 = recoveryReader.Current.Key;

                        var get2 = await client.GetAsync<JobItem>(key2);
                        if (!get2.IsSuccess || !get2.Version.HasValue)
                            continue;

                        var lockKey2 = context.Key($"job:lock:{get2.Value!.Id}");
                        var lockCheck = await client.GetAsync<string>(lockKey2);

                        if (!lockCheck.IsSuccess)
                        {
                            await client.PutAsync(
                                key2,
                                get2.Value!,
                                Put.WithIfMatch(get2.Version.Value)
                                   .WithEntity(Entity)
                                   .WithLabel("status", "pending"));

                            Console.WriteLine($"{workerName} recovered orphan invoice #{get2.Value.InvoiceNumber}");
                        }
                    }

                    await Task.Delay(300);
                }
            }
        }

        // ============================================================
        // STATUS COUNT
        // ============================================================

        private static async Task<int> CountByStatusAsync(IDkv dkv, string status)
        {
            var query = SearchQuery
                .For("job")
                .Eq("status", status);

            int count = 0;

            await using var reader =
                await (await dkv.Scan.SearchAsync(query)).AsEntries();

            while (await reader.ReadAsync())
                count++;

            return count;
        }
    }

    public sealed class JobItem
    {
        public Guid Id { get; set; }
        public int InvoiceNumber { get; set; }
    }
}