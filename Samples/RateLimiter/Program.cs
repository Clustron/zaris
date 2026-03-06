using System;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.DKV.Client.Helpers;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.RateLimiter
{
    internal class Program
    {
        private const int MaxRequests = 5;
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Distributed Rate Limiter Sample");

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

            var context = new SampleContext("rate-limit");
            var userId = "user-42";

            ConsoleHelper.Section("Simulating 10 Requests (limit = 5 per 10 sec)");

            for (int i = 1; i <= 10; i++)
            {
                bool allowed = await IsAllowedAsync(client, context, userId);

                if (allowed)
                    ConsoleHelper.Success($"Request {i}  ALLOWED");
                else
                    ConsoleHelper.Error($"Request {i}  BLOCKED");

                await Task.Delay(800); // simulate traffic
            }

            ConsoleHelper.Success("\nRate limiter sample completed.");
        }

        private static async Task<bool> IsAllowedAsync(
            IDkvClient client,
            SampleContext context,
            string userId)
        {
            var windowKey = GetWindowKey(context, userId);

            var counters = ((IDkv)client).Counters;

            var result = await counters.AddAsync(
                windowKey,
                1,
                new CounterOptions
                {
                    Ttl = Window
                });

            if (!result.IsSuccess)
                return false;

            return result.Value.Current <= MaxRequests;
        }

        private static string GetWindowKey(
            SampleContext context,
            string userId)
        {
            var now = DateTime.UtcNow;
            var windowStart = now
                .AddSeconds(-(now.Second % Window.TotalSeconds))
                .AddMilliseconds(-now.Millisecond);

            return context.Key(
                $"rate:{userId}:{windowStart:yyyyMMddHHmmss}");
        }
    }
}