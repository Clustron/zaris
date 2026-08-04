using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;
using Clustron.Zaris.Samples.Shared.Models;

namespace Clustron.Zaris.Sample.Bulk;

internal class BulkSampleApp
{
    private readonly IZarisClientProvider _provider;

    // Must match appsettings.json → ConnectionStrings:<name>
    private const string StoreName = "teststore";

    public BulkSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        // -----------------------------------------------------
        // Resolve client
        // -----------------------------------------------------
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        // Optional: clean start
        await client.ClearAsync();

        var context = new SampleContext("bulk");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // ============================================================
        // BULK PUT
        // ============================================================

        ConsoleHelper.Section("Bulk PUT");

        var customers = Enumerable.Range(1, 5)
            .ToDictionary(
                i => context.Key($"customer:{i}"),
                i => new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = $"Customer {i}",
                    Email = $"bulk{i}@demo.com"
                });

        var bulkPut = await client.PutManyAsync(customers);

        if (bulkPut.All(r => r.IsSuccess))
            ConsoleHelper.Success("Bulk PUT succeeded for all items.");
        else
            ConsoleHelper.Error("Some items failed during Bulk PUT.");

        Console.WriteLine($"Current Count: {client.Count}");

        // ============================================================
        // BULK GET
        // ============================================================

        ConsoleHelper.Section("Bulk GET");

        var keys = customers.Keys.ToList();
        var bulkGet = await client.GetManyAsync<Customer>(keys);

        for (int i = 0; i < bulkGet.Count; i++)
        {
            var result = bulkGet[i];

            if (result.IsSuccess)
                Console.WriteLine($"[{i}] {result.Value?.Email}");
            else
                Console.WriteLine($"[{i}] FAILED ({result.Status})");
        }

        // ============================================================
        // BULK DELETE
        // ============================================================

        ConsoleHelper.Section("Bulk DELETE");

        var bulkDelete = await client.DeleteManyAsync(keys);

        if (bulkDelete.All(r => r.IsSuccess))
            ConsoleHelper.Success("Bulk DELETE succeeded for all items.");
        else
            ConsoleHelper.Error("Some items failed during Bulk DELETE.");

        // ============================================================
        // VERIFY DELETE
        // ============================================================

        Console.WriteLine("Verification after delete:");

        var verify = await client.GetManyAsync<Customer>(keys);

        foreach (var r in verify)
            Console.WriteLine($"Status: {r.Status}");

        Console.WriteLine($"Final Count: {client.Count}");

        // ============================================================
        // CLEANUP
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}