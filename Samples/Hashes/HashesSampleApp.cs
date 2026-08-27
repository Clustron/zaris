using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Hashes;

/// <summary>
/// Demonstrates the native Zaris hash data structure (<c>IZaris.Hashes</c> / IHashClient).
///
/// A hash is a single key holding many named fields — ideal for records like a user
/// profile. Field values are stored as raw <c>byte[]</c>, so string values are encoded
/// with UTF-8 on the way in and decoded on the way out.
/// </summary>
internal class HashesSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public HashesSampleApp(IZarisClientProvider provider)
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

        await client.ClearAsync();

        var context = new SampleContext("hashes");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // Sub-clients live on IZaris, so cast the resolved client once.
        var hashes = ((IZaris)client).Hashes;

        var userKey = context.Key("user:1");

        // ============================================================
        // Set fields (single + many)
        // ============================================================

        ConsoleHelper.Section("Set Fields");

        var setName = await hashes.SetAsync(userKey, "name", Encoding.UTF8.GetBytes("Ada Lovelace"));
        Console.WriteLine($"Set 'name' → IsNewField: {setName.Value} (Status: {setName.Status})");

        var setMany = await hashes.SetAsync(userKey, new[]
        {
            new KeyValuePair<string, byte[]>("email", Encoding.UTF8.GetBytes("ada@example.com")),
            new KeyValuePair<string, byte[]>("city",  Encoding.UTF8.GetBytes("London")),
            new KeyValuePair<string, byte[]>("logins", Encoding.UTF8.GetBytes("0")),
        });
        Console.WriteLine($"Set 3 more fields → new fields added: {setMany.Value}");

        // SetIfMissing only writes when the field is absent (like Redis HSETNX).
        var raceCity = await hashes.SetIfMissingAsync(userKey, "city", Encoding.UTF8.GetBytes("Paris"));
        Console.WriteLine($"SetIfMissing 'city' (already set) → wrote: {raceCity.Value} (expected False)");

        // ============================================================
        // Read the whole hash
        // ============================================================

        ConsoleHelper.Section("Get All Fields");

        var all = await hashes.GetAllAsync(userKey);
        if (all.IsSuccess)
        {
            foreach (var field in all.Value)
                Console.WriteLine($"  {field.Key} = {Encoding.UTF8.GetString(field.Value)}");
        }
        else
        {
            ConsoleHelper.Error($"GetAll failed: {all.Status}");
        }

        // ============================================================
        // Read a single field
        // ============================================================

        ConsoleHelper.Section("Get Single Field");

        var email = await hashes.GetAsync(userKey, "email");
        if (email.IsSuccess && email.Value is not null)
            ConsoleHelper.Success($"email = {Encoding.UTF8.GetString(email.Value)}");
        else
            ConsoleHelper.Error($"GET 'email' failed: {email.Status}");

        // ============================================================
        // Field existence + length
        // ============================================================

        ConsoleHelper.Section("Exists / Length");

        var exists = await hashes.ExistsAsync(userKey, "email");
        Console.WriteLine($"Exists 'email' → {exists.Value}");

        var missing = await hashes.ExistsAsync(userKey, "phone");
        Console.WriteLine($"Exists 'phone' → {missing.Value}");

        var length = await hashes.LengthAsync(userKey);
        Console.WriteLine($"Field count → {length.Value}");

        // ============================================================
        // Atomic numeric field
        // ============================================================

        ConsoleHelper.Section("IncrementBy");

        var inc1 = await hashes.IncrementByAsync(userKey, "logins", 1);
        Console.WriteLine($"logins after +1 → {inc1.Value}");

        var inc2 = await hashes.IncrementByAsync(userKey, "logins", 4);
        Console.WriteLine($"logins after +4 → {inc2.Value}");

        // ============================================================
        // Delete a field
        // ============================================================

        ConsoleHelper.Section("Delete Field");

        var deleted = await hashes.DeleteAsync(userKey, new[] { "city" });
        Console.WriteLine($"Deleted 'city' → fields removed: {deleted.Value}");

        var afterDelete = await hashes.GetAllAsync(userKey);
        if (afterDelete.IsSuccess)
        {
            Console.WriteLine("Remaining fields:");
            foreach (var field in afterDelete.Value)
                Console.WriteLine($"  {field.Key} = {Encoding.UTF8.GetString(field.Value)}");
        }

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}
