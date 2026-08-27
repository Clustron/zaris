using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Abstractions.Collections;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Streams;

/// <summary>
/// Demonstrates the native Zaris Streams API (<see cref="IStreamClient"/>): an append-only,
/// replicated event log with consumer groups — the same model as Redis Streams, reached through
/// the typed .NET client. Uses an "orders" stream: append events, read them back, then process
/// them through a consumer group with acknowledgements.
/// </summary>
internal class StreamsSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public StreamsSampleApp(IZarisClientProvider provider)
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

        var context = new SampleContext("streams");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // Sub-clients live on IZaris (the derived interface); cast to reach them.
        var streams = ((IZaris)client).Streams;

        var stream = context.Key("orders");

        // ============================================================
        // Append events
        // ============================================================

        ConsoleHelper.Section("Append events");

        var e1 = await streams.AddAsync(stream, Fields(("id", "1001"), ("total", "59.90"), ("status", "placed")));
        var e2 = await streams.AddAsync(stream, Fields(("id", "1002"), ("total", "12.00"), ("status", "placed")));
        var e3 = await streams.AddAsync(stream, Fields(("id", "1003"), ("total", "410.50"), ("status", "placed")));

        Console.WriteLine($"Appended → {e1.Value}, {e2.Value}, {e3.Value}");

        // ============================================================
        // Length + Range
        // ============================================================

        ConsoleHelper.Section("Length + Range");

        var len = await streams.LengthAsync(stream);
        ConsoleHelper.Info($"Stream length: {len.Value}");

        var range = await streams.RangeAsync(stream, "-", "+");
        if (range.IsSuccess)
        {
            foreach (var entry in range.Value)
                Console.WriteLine($"  {Format(entry)}");
        }
        else
        {
            ConsoleHelper.Error($"RANGE failed: {range.Status}");
        }

        // ============================================================
        // Read after id
        // ============================================================

        ConsoleHelper.Section("Read after id");

        // "0" is before every generated id, so this reads the whole stream from the start.
        var read = await streams.ReadAsync(stream, "0", count: 10);
        if (read.IsSuccess)
            ConsoleHelper.Success($"Read {read.Value.Count} entries after id 0");
        else
            ConsoleHelper.Error($"READ failed: {read.Status}");

        // ============================================================
        // Consumer group
        // ============================================================

        ConsoleHelper.Section("Consumer group");

        // Start the group at "0" so it can claim the events we already appended.
        var groupCreate = await streams.GroupCreateAsync(stream, "fulfilment", startId: "0", mkStream: false);
        ConsoleHelper.Info($"Group 'fulfilment' created: {groupCreate.IsSuccess}");

        // worker-1 pulls new (unassigned) messages for the group with ">".
        var claimed = await streams.ReadGroupAsync(stream, "fulfilment", "worker-1", id: ">", count: 10);
        if (claimed.IsSuccess)
        {
            ConsoleHelper.Info($"worker-1 claimed {claimed.Value.Count} messages:");
            foreach (var entry in claimed.Value)
                Console.WriteLine($"  {Format(entry)}");

            // Acknowledge the whole batch so it leaves the pending list.
            var ids = claimed.Value.Select(e => e.Id.ToString()).ToList();
            var ack = await streams.AckAsync(stream, "fulfilment", ids);
            ConsoleHelper.Success($"Acknowledged {ack.Value} messages");
        }
        else
        {
            ConsoleHelper.Error($"READGROUP failed: {claimed.Status}");
        }

        // ============================================================
        // Info
        // ============================================================

        ConsoleHelper.Section("Info");

        var info = await streams.InfoAsync(stream);
        var lenAfter = await streams.LengthAsync(stream);
        ConsoleHelper.Info($"Info retrieved: {info.IsSuccess} · length now: {lenAfter.Value}");

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }

    // Build a stream entry's fields from (name, value) pairs, encoding values as UTF-8 bytes.
    private static IEnumerable<KeyValuePair<string, byte[]>> Fields(params (string Name, string Value)[] pairs)
        => pairs.Select(p => new KeyValuePair<string, byte[]>(p.Name, Encoding.UTF8.GetBytes(p.Value)));

    // Render an entry as "<id> [k=v, k=v]" with UTF-8-decoded field values.
    private static string Format(StreamEntry entry)
    {
        var fields = entry.Fields.Select(f => $"{f.Key}={Encoding.UTF8.GetString(f.Value)}");
        return $"{entry.Id} [{string.Join(", ", fields)}]";
    }
}
