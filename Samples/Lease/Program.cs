using System;
using System.Linq;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.Lease
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Lease Sample");

            // 1️⃣ Load configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()
                ?? throw new InvalidOperationException("Missing Dkv configuration.");

            var mode = options.GetMode();

            var client = await DKVClient.Initialize(
                clusterId: options.ClusterId,
                mode: mode,
                remoteHost: mode == DkvClientMode.Remote ? options.RemoteHost : null,
                remotePort: mode == DkvClientMode.Remote ? options.RemotePort : 0,
                logFilePath: options.LogFilePath
            );

            ConsoleHelper.Success("Connected to cluster.");

            var context = new SampleContext("lease");
            var leases = ((IDkv)client).Leases;

            // ============================================================
            // 1️⃣ Grant Lease & Attach Keys
            // ============================================================

            ConsoleHelper.Section("Grant Lease (30 seconds)");

            var lease = await leases.GrantAsync(TimeSpan.FromSeconds(30));

            if (!lease.IsSuccess)
            {
                ConsoleHelper.Error("Lease grant failed.");
                return;
            }

            ConsoleHelper.Success($"Lease Granted: {lease.Value}");

            var keys = Enumerable.Range(1, 5)
                .Select(i => context.Key($"lease-key-{i}"))
                .ToList();

            foreach (var key in keys)
            {
                await client.PutAsync(key, $"value-{key}", Put.WithLease(lease.Value));
            }

            ConsoleHelper.Info($"Inserted {keys.Count} keys bound to lease.");
            Console.WriteLine($"Current Count: {client.Count}");

            // ============================================================
            // 2️⃣ Let Lease Expire
            // ============================================================

            ConsoleHelper.Section("Waiting For Lease Expiry");
            ConsoleHelper.Info("Waiting 35 seconds for lease to expire...");

            await Task.Delay(TimeSpan.FromSeconds(35));

            var check = await client.GetAsync<string>(keys.First());

            Console.WriteLine($"After Expiry → Status: {check.Status}");
            Console.WriteLine($"Current Count: {client.Count}");

            // ============================================================
            // 3️⃣ KeepAlive Demo
            // ============================================================

            ConsoleHelper.Section("KeepAlive Demonstration");

            var lease2 = await leases.GrantAsync(TimeSpan.FromSeconds(30));
            ConsoleHelper.Success($"Lease2 Granted: {lease2.Value}");

            var keepKey = context.Key("keepalive-key");
            await client.PutAsync(keepKey, "keepalive", Put.WithLease(lease2.Value));

            ConsoleHelper.Info("Waiting 20 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(20));

            ConsoleHelper.Info("Sending KeepAlive...");
            await leases.KeepAliveAsync(lease2.Value);

            ConsoleHelper.Info("Waiting another 20 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(20));

            var stillThere = await client.GetAsync<string>(keepKey);
            Console.WriteLine($"After KeepAlive → Status: {stillThere.Status}");

            // ============================================================
            // 4️⃣ Revoke Demo
            // ============================================================

            ConsoleHelper.Section("Revoke Demonstration");

            var lease3 = await leases.GrantAsync(TimeSpan.FromSeconds(30));
            ConsoleHelper.Success($"Lease3 Granted: {lease3.Value}");

            var revokeKeys = Enumerable.Range(1, 3)
                .Select(i => context.Key($"revoke-key-{i}"))
                .ToList();

            foreach (var k in revokeKeys)
                await client.PutAsync(k, "revoke", Put.WithLease(lease3.Value));

            Console.WriteLine($"Before Revoke → Count: {client.Count}");

            await leases.RevokeAsync(lease3.Value);

            var revokedCheck = await client.GetAsync<string>(revokeKeys.First());
            Console.WriteLine($"After Revoke → Status: {revokedCheck.Status}");
            Console.WriteLine($"After Revoke → Count: {client.Count}");

            ConsoleHelper.Success("Lease sample completed.");

            Console.WriteLine("\nDone.");
        }
    }
}
