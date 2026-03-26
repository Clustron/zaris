using System;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.Dkv.Samples.Shared;

namespace Clustron.Dkv.Sample.RateLimiter;

internal class RateLimiterSampleApp
{
    private readonly IDkvClientProvider _provider;

    private const string StoreName = "teststore";
    private const int MaxRequests = 5;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    public RateLimiterSampleApp(IDkvClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        var context = new SampleContext("rate-limit");
        var userId = "user-42";

        ConsoleHelper.Section("Simulating Requests");
        ConsoleHelper.Info($"Limit = {MaxRequests} per {Window.TotalSeconds} seconds");

        for (int i = 1; i <= 10; i++)
        {
            bool allowed = await IsAllowedAsync(client, context, userId);

            if (allowed)
                ConsoleHelper.Success($"Request {i}  ALLOWED");
            else
                ConsoleHelper.Error($"Request {i}  BLOCKED");

            await Task.Delay(800);
        }

        ConsoleHelper.Success("\nRate limiter sample completed.");
    }

    // ============================================================
    // RATE LIMIT LOGIC
    // ============================================================

    private async Task<bool> IsAllowedAsync(
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