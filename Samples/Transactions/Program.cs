using System;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Abstractions.Transactions;
using Clustron.DKV.Client;
using Clustron.Dkv.Samples.Shared;
using Clustron.DKV.Client.Helpers;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.TransactionsBasic
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Basic Transaction Sample");

            // ============================================================
            //  Load configuration
            // ============================================================

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

            var context = new SampleContext("tx-basic");

            var keyA = context.Key("keyA");
            var keyB = context.Key("keyB");

            // ============================================================
            // Setup initial data
            // ============================================================

            ConsoleHelper.Section("Initializing Data");

            await client.PutAsync(keyA, 10);
            await client.PutAsync(keyB, 20);

            Console.WriteLine($"{keyA} = 10");
            Console.WriteLine($"{keyB} = 20");

            // ============================================================
            // SUCCESSFUL TRANSACTION
            // ============================================================

            ConsoleHelper.Section("Successful Transaction");

            await using (var tx = await client.BeginTransactionAsync())
            {
                var a = await tx.GetAsync<int>(keyA);
                var b = await tx.GetAsync<int>(keyB);

                Console.WriteLine($"Read inside TX => A={a.Value}, B={b.Value}");

                await tx.PutAsync(keyA, a.Value + 5);
                await tx.PutAsync(keyB, b.Value + 5);

                var result = await tx.CommitAsync();

                if (result.IsSuccess)
                    ConsoleHelper.Success("Transaction committed.");
                else
                    ConsoleHelper.Error("Transaction failed.");
            }

            var afterA = await client.GetAsync<int>(keyA);
            var afterB = await client.GetAsync<int>(keyB);

            Console.WriteLine($"After Commit => A={afterA.Value}, B={afterB.Value}");

            // ============================================================
            // ROLLBACK EXAMPLE
            // ============================================================

            ConsoleHelper.Section("Rollback Example");

            await using (var tx = await client.BeginTransactionAsync())
            {
                await tx.PutAsync(keyA, 999);

                Console.WriteLine("Updated A inside TX → 999");

                await tx.RollbackAsync();
            }

            var afterRollback = await client.GetAsync<int>(keyA);

            Console.WriteLine($"After Rollback => A={afterRollback.Value}");

            // ============================================================
            // CONFLICT EXAMPLE
            // ============================================================

            ConsoleHelper.Section("Conflict Example");

            await using (var tx = await client.BeginTransactionAsync())
            {
                var value = await tx.GetAsync<int>(keyA);

                Console.WriteLine($"TX Read A = {value.Value}");

                // External update causing conflict
                await client.PutAsync(keyA, 500);

                await tx.PutAsync(keyA, value.Value + 1);

                var result = await tx.CommitAsync();

                if (!result.IsSuccess)
                    ConsoleHelper.Error("Transaction failed due to conflict.");
                else
                    ConsoleHelper.Error("Unexpected success.");
            }

            var final = await client.GetAsync<int>(keyA);

            Console.WriteLine($"Final value of A = {final.Value}");

            // ============================================================
            // DELETE IN TRANSACTION
            // ============================================================

            ConsoleHelper.Section("Delete Inside Transaction");

            await using (var tx = await client.BeginTransactionAsync())
            {
                await tx.DeleteAsync(keyB);

                var inside = await tx.GetAsync<int>(keyB);

                Console.WriteLine($"Inside TX => Exists = {inside.IsSuccess}");

                await tx.CommitAsync();
            }

            var afterDelete = await client.GetAsync<int>(keyB);

            Console.WriteLine($"After Commit => Exists = {afterDelete.IsSuccess}");

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