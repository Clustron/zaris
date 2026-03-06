using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.DKV.Client.Helpers;
using Clustron.Dkv.Samples.Shared;
using Clustron.Dkv.Samples.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.Bulk
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Bulk Operations Sample");

            //  Load configuration
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

            var context = new SampleContext("bulk");

            ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

            // ============================================================
            //  Bulk Insert
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
            // Bulk GET
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
            //  Bulk DELETE
            // ============================================================

            ConsoleHelper.Section("Bulk DELETE");

            var bulkDelete = await client.DeleteManyAsync(keys);

            if (bulkDelete.All(r => r.IsSuccess))
                ConsoleHelper.Success("Bulk DELETE succeeded for all items.");
            else
                ConsoleHelper.Error("Some items failed during Bulk DELETE.");

            var verify = await client.GetManyAsync<Customer>(keys);

            Console.WriteLine("Verification after delete:");
            foreach (var r in verify)
                Console.WriteLine($"Status: {r.Status}");

            Console.WriteLine($"Final Count: {client.Count}");

            // ============================================================
            // Cleanup
            // ============================================================

            ConsoleHelper.Section("Cleanup");

            await client.ClearAsync(new ClearRequest(context.Prefix));

            ConsoleHelper.Success("Cleanup completed.");

            Console.WriteLine("\nDone.");
        }
    }
}