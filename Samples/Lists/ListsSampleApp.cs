using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Lists;

/// <summary>
/// Demonstrates the native Zaris list data structure (<c>IZaris.Lists</c>) using a work-queue
/// as the running example: producers push tasks onto the tail, a worker pops from the head.
/// List elements are raw <c>byte[]</c>, so this sample encodes/decodes with UTF-8.
/// </summary>
internal class ListsSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public ListsSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);
    private static string Dec(byte[] b) => Encoding.UTF8.GetString(b);

    public async Task RunAsync()
    {
        // -----------------------------------------------------
        // Resolve client
        // -----------------------------------------------------
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        await client.ClearAsync();

        var context = new SampleContext("lists");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // Sub-clients live on IZaris; the provider hands back the base IZarisClient, so cast once.
        var lists = ((IZaris)client).Lists;

        var queueKey = context.Key("queue");

        // ============================================================
        // Enqueue (push onto the tail)
        // ============================================================

        ConsoleHelper.Section("Enqueue — PushRight");

        var pushed = await lists.PushRightAsync(
            queueKey,
            new[] { Enc("job-1"), Enc("job-2"), Enc("job-3") });

        if (pushed.IsSuccess)
            ConsoleHelper.Success($"Pushed 3 jobs → list length is now {pushed.Value}");
        else
            ConsoleHelper.Error($"PushRight failed: {pushed.Status}");

        // A late-arriving, higher-priority job jumps the queue via the head.
        var jump = await lists.PushLeftAsync(queueKey, new[] { Enc("job-urgent") });
        Console.WriteLine($"PushLeft job-urgent → length {jump.Value}");

        // ============================================================
        // Inspect — Length + Range
        // ============================================================

        ConsoleHelper.Section("Inspect — Length + Range");

        var length = await lists.LengthAsync(queueKey);
        Console.WriteLine($"Length: {length.Value}");

        var all = await lists.RangeAsync(queueKey, 0, -1); // 0..-1 = the whole list
        Console.WriteLine("Contents (head → tail): " +
            string.Join(", ", all.Value.Select(Dec)));

        // ============================================================
        // Random access — Index + Set
        // ============================================================

        ConsoleHelper.Section("Random access — Index + Set");

        var head = await lists.IndexAsync(queueKey, 0);
        Console.WriteLine($"Index[0]: {(head.Value is null ? "<none>" : Dec(head.Value))}");

        var set = await lists.SetAsync(queueKey, 1, Enc("job-1-rescheduled"));
        ConsoleHelper.Info($"Set index 1 → {(set.IsSuccess ? "ok" : set.Status.ToString())}");

        var afterSet = await lists.RangeAsync(queueKey, 0, -1);
        Console.WriteLine("After Set: " + string.Join(", ", afterSet.Value.Select(Dec)));

        // ============================================================
        // Dequeue — PopLeft (FIFO) and PopRight (LIFO)
        // ============================================================

        ConsoleHelper.Section("Dequeue — PopLeft / PopRight");

        var popLeft = await lists.PopLeftAsync(queueKey);
        Console.WriteLine($"PopLeft (process next): {string.Join(", ", popLeft.Value.Select(Dec))}");

        var popRight = await lists.PopRightAsync(queueKey);
        Console.WriteLine($"PopRight (take newest): {string.Join(", ", popRight.Value.Select(Dec))}");

        var remaining = await lists.RangeAsync(queueKey, 0, -1);
        Console.WriteLine("Remaining: " + string.Join(", ", remaining.Value.Select(Dec)));

        // ============================================================
        // Remove by value
        // ============================================================

        ConsoleHelper.Section("Remove by value");

        var removed = await lists.RemoveAsync(queueKey, count: 1, Enc("job-3"));
        Console.WriteLine($"Removed {removed.Value} occurrence(s) of 'job-3'");

        var final = await lists.RangeAsync(queueKey, 0, -1);
        Console.WriteLine("Final: " + string.Join(", ", final.Value.Select(Dec)));

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}
