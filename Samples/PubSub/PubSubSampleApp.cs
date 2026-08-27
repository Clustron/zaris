using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.PubSub;

internal class PubSubSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    // Received-message counters. The subscription callbacks fire on background
    // threads, so every mutation goes through Interlocked.
    private int _exactReceived;
    private int _patternReceived;

    public PubSubSampleApp(IZarisClientProvider provider)
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

        var context = new SampleContext("pubsub");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // Pub/Sub lives on IZaris (the full client surface), so cast to reach it.
        var ps = ((IZaris)client).PubSub;

        // ============================================================
        // Subscribe (exact channel)
        // ============================================================

        ConsoleHelper.Section("Subscribe");

        var channel = context.Key("news");

        var subscription = await ps.SubscribeAsync(
            new[] { channel },
            msg =>
            {
                Interlocked.Increment(ref _exactReceived);
                var text = Encoding.UTF8.GetString(msg.Payload);
                ConsoleHelper.Info($"received on {msg.Channel}: {text}");
                return Task.CompletedTask;
            });

        ConsoleHelper.Success($"Subscribed to channel: {channel}");

        // ============================================================
        // Publish
        // ============================================================

        ConsoleHelper.Section("Publish");

        for (var i = 1; i <= 3; i++)
        {
            var delivered = await ps.PublishAsync(
                channel,
                Encoding.UTF8.GetBytes($"headline #{i}"));

            Console.WriteLine($"Published headline #{i} → delivered to {delivered} subscriber(s)");
        }

        // Delivery is asynchronous — poll briefly instead of a fixed sleep.
        await WaitUntilAsync(() => Volatile.Read(ref _exactReceived) >= 3);

        ConsoleHelper.Success($"Delivered {Volatile.Read(ref _exactReceived)}/3 messages to the subscriber.");

        // ============================================================
        // Pattern subscribe (glob-style channel match)
        // ============================================================

        ConsoleHelper.Section("Pattern subscribe");

        var pattern = context.Key("orders.*");

        var patternSubscription = await ps.PSubscribeAsync(
            new[] { pattern },
            msg =>
            {
                Interlocked.Increment(ref _patternReceived);
                var text = Encoding.UTF8.GetString(msg.Payload);
                ConsoleHelper.Info($"pattern '{msg.Pattern}' matched channel '{msg.Channel}': {text}");
                return Task.CompletedTask;
            });

        ConsoleHelper.Success($"Pattern-subscribed to: {pattern}");

        var ordersChannel = context.Key("orders.created");
        await ps.PublishAsync(ordersChannel, Encoding.UTF8.GetBytes("order 1001 created"));

        await WaitUntilAsync(() => Volatile.Read(ref _patternReceived) >= 1);

        ConsoleHelper.Success($"Pattern subscriber received {Volatile.Read(ref _patternReceived)} message(s).");

        // ============================================================
        // Unsubscribe
        // ============================================================

        ConsoleHelper.Section("Unsubscribe");

        await subscription.DisposeAsync();
        await patternSubscription.DisposeAsync();

        ConsoleHelper.Success("Unsubscribed from both subscriptions.");

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }

    // Polls a condition up to ~2s (40 × 50ms) so the sample stays fast and
    // deterministic rather than hard-sleeping for a fixed interval.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }
    }
}
