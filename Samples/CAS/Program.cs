using System;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.DKV.Client.Helpers;
using Clustron.Dkv.Samples.Shared;
using Clustron.Dkv.Samples.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.CAS
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Compare-And-Swap (CAS) Sample");

            //  Load configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()
                ?? throw new InvalidOperationException("Missing Dkv configuration.");

            IDkvClient client = options.GetMode() == DkvClientMode.Remote
                ? await DKVClient.InitializeRemote(options.ClusterId, options.Seeds!, options.LogFilePath)
                : await DKVClient.InitializeInProc(options.ClusterId, options.LogFilePath);

            ConsoleHelper.Success("Connected to cluster.");

            var context = new SampleContext("cas");

            var key = context.Key("customer:1");

            // ============================================================
            //  Initial Insert (IfAbsent)
            // ============================================================

            ConsoleHelper.Section("Initial Insert (IfAbsent)");

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = "John Doe",
                Email = "user1@demo.com"
            };

            var put1 = await client.PutAsync(key, customer, Put.IfAbsent());

            if (put1.IsSuccess)
                ConsoleHelper.Success("Initial insert succeeded.");
            else
                ConsoleHelper.Error($"Insert failed: {put1.Status}");

            // ============================================================
            // Read Value + ItemVersion
            // ============================================================

            ConsoleHelper.Section("Read Current Version");

            var get1 = await client.GetAsync<Customer>(key);

            if (!get1.IsSuccess || !get1.Version.HasValue)
            {
                ConsoleHelper.Error("GET failed or version missing.");
                return;
            }

            var currentVersion = get1.Version.Value;

            Console.WriteLine($"Name:        {get1.Value!.Name}");
            Console.WriteLine($"Email:       {get1.Value.Email}");
            Console.WriteLine($"ItemVersion: {currentVersion.Version}");

            // ============================================================
            //  Successful CAS Update
            // ============================================================

            ConsoleHelper.Section("Successful CAS Update");

            get1.Value.Email = "user2@demo.com";

            var put2 = await client.PutAsync(
                key,
                get1.Value,
                Put.WithIfMatch(currentVersion));

            if (put2.IsSuccess)
            {
                ConsoleHelper.Success("CAS update succeeded.");
                Console.WriteLine($"New Version: {put2.Version?.Version}");
            }
            else
            {
                ConsoleHelper.Error($"CAS failed: {put2.Status}");
            }

            var latestVersion = put2.Version!.Value;

            // ============================================================
            //  Failed CAS (Using Stale Version)
            // ============================================================

            ConsoleHelper.Section("Failed CAS (Stale Version)");

            var staleAttempt = await client.PutAsync(
                key,
                get1.Value,
                Put.WithIfMatch(currentVersion)); // old version

            if (!staleAttempt.IsSuccess && staleAttempt.Status == KvStatus.Conflict)
                ConsoleHelper.Success("Conflict correctly detected for stale version.");
            else
                ConsoleHelper.Error("Unexpected CAS behavior.");

            // ============================================================
            //  CAS Delete
            // ============================================================

            ConsoleHelper.Section("CAS Delete");

            var delete = await client.DeleteAsync(
                key,
                Delete.IfMatch(latestVersion));

            if (delete.IsSuccess)
                ConsoleHelper.Success("Delete succeeded with correct version.");
            else
                ConsoleHelper.Error($"Delete failed: {delete.Status}");

            // Confirm deletion
            var verify = await client.GetAsync<Customer>(key);
            Console.WriteLine($"GET after delete, Status: {verify.Status}");

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