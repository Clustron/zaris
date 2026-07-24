using System;
using System.Threading.Tasks;
using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Client;
using Clustron.Dictus.Client.Helpers;
using Clustron.Dictus.Samples.Shared;

namespace Clustron.Dictus.Sample.SimpleEnterpriseQueue;

internal class SimpleQueueSampleApp
{
    private readonly IDictusClientProvider _provider;

    private const string StoreName = "teststore";
    private const string Entity = "job";
    private const int TotalJobs = 10;

    public SimpleQueueSampleApp(IDictusClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        await client.ClearAsync();

        var context = new SampleContext("simple-queue");

        await ProduceJobs(client, context);

        ConsoleHelper.Section("Starting Workers");

        await Task.WhenAll(
            RunWorkerAsync(client, context, "worker-1"),
            RunWorkerAsync(client, context, "worker-2"),
            RunWorkerAsync(client, context, "worker-3")
        );

        ConsoleHelper.Success("\nQueue processing completed.");

        // Authoritative result
        var completedCount = await CountByStatusAsync((IDictus)client, "completed");

        Console.WriteLine();
        Console.WriteLine($"Total Completed Jobs: {completedCount}");

        // Cleanup
        ConsoleHelper.Section("Cleanup");
        await client.ClearAsync(new ClearRequest(context.Prefix));
        ConsoleHelper.Success("Sample cleanup completed.");
    }

    // ============================================================
    // PRODUCER
    // ============================================================

    private async Task ProduceJobs(IDictusClient client, SampleContext context)
    {
        ConsoleHelper.Section("Producing Jobs");

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

    private async Task RunWorkerAsync(
        IDictusClient client,
        SampleContext context,
        string workerName)
    {
        var dictus = (IDictus)client;

        var leaseResult = await dictus.Leases.GrantAsync(TimeSpan.FromSeconds(5));

        if (!leaseResult.IsSuccess)
            return;

        var lease = leaseResult.Value;

        while (true)
        {
            var completedCount = await CountByStatusAsync(dictus, "completed");

            if (completedCount >= TotalJobs)
            {
                Console.WriteLine($"{workerName} exiting - all jobs completed.");
                return;
            }

            // -----------------------------------------------------
            // Find pending job
            // -----------------------------------------------------
            var query = SearchQuery
                .For(Entity)
                .Eq("status", "pending")
                .Limit(1);

            await using var reader =
                await (await dictus.Scan.SearchAsync(query)).AsEntries();

            if (await reader.ReadAsync())
            {
                var key = reader.Current.Key;

                var get = await client.GetAsync<JobItem>(key);
                if (!get.IsSuccess || !get.Version.HasValue)
                    continue;

                if (!get.Metadata.Labels.TryGetValue("status", out var status) ||
                    status.Value != "pending")
                    continue;

                // -----------------------------------------------------
                // CAS → processing
                // -----------------------------------------------------
                var cas = await client.PutAsync(
                    key,
                    get.Value!,
                    Put.WithIfMatch(get.Version.Value)
                       .WithEntity(Entity)
                       .WithLabel("status", "processing"));

                if (!cas.IsSuccess)
                    continue;

                // -----------------------------------------------------
                // Lock via lease
                // -----------------------------------------------------
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

                // -----------------------------------------------------
                // Simulated failure
                // -----------------------------------------------------
                if (Random.Shared.Next(0, 6) == 0)
                {
                    Console.WriteLine($"{workerName} FAILED invoice #{invoice}.");

                    await client.PutAsync(
                        key,
                        get.Value!,
                        Put.WithIfMatch(cas.Version!.Value)
                           .WithEntity(Entity)
                           .WithLabel("status", "pending"));

                    await client.DeleteAsync(lockKey);
                    continue;
                }

                // -----------------------------------------------------
                // Complete
                // -----------------------------------------------------
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
                // -----------------------------------------------------
                // Recovery
                // -----------------------------------------------------
                var recoveryQuery = SearchQuery
                    .For(Entity)
                    .Eq("status", "processing")
                    .Limit(1);

                await using var recoveryReader =
                    await (await dictus.Scan.SearchAsync(recoveryQuery)).AsEntries();

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

                        Console.WriteLine($"{workerName} recovered invoice #{get2.Value.InvoiceNumber}");
                    }
                }

                await Task.Delay(300);
            }
        }
    }

    // ============================================================
    // STATUS COUNT
    // ============================================================

    private async Task<int> CountByStatusAsync(IDictus dictus, string status)
    {
        var query = SearchQuery
            .For("job")
            .Eq("status", status);

        int count = 0;

        await using var reader =
            await (await dictus.Scan.SearchAsync(query)).AsEntries();

        while (await reader.ReadAsync())
            count++;

        return count;
    }
}

// ============================================================
// MODEL
// ============================================================

public sealed class JobItem
{
    public Guid Id { get; set; }
    public int InvoiceNumber { get; set; }
}