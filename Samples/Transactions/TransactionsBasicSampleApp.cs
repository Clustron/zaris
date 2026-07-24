using System;
using System.Threading.Tasks;
using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Abstractions.Transactions;
using Clustron.Dictus.Client;
using Clustron.Dictus.Client.Helpers;
using Clustron.Dictus.Samples.Shared;

namespace Clustron.Dictus.Sample.TransactionsBasic;

internal class TransactionsBasicSampleApp
{
    private readonly IDictusClientProvider _provider;

    private const string StoreName = "teststore";

    public TransactionsBasicSampleApp(IDictusClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        var context = new SampleContext("tx-basic");

        var keyA = context.Key("keyA");
        var keyB = context.Key("keyB");

        // ============================================================
        // Initialize Data
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
        // ROLLBACK
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
        // CONFLICT
        // ============================================================

        ConsoleHelper.Section("Conflict Example");

        await using (var tx = await client.BeginTransactionAsync())
        {
            var value = await tx.GetAsync<int>(keyA);

            Console.WriteLine($"TX Read A = {value.Value}");

            // External update
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
        // DELETE
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
        // CLEANUP
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}