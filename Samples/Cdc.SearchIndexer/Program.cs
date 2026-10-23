// Clustron Zaris – Change Data Capture (CDC) sample: secondary index / search sync.
//
// Consume the Zaris change feed and maintain a tiny in-memory inverted index over plain-string
// values: a Put (re)indexes the document's terms; a Delete removes the document. Keyed by the
// document key, so re-delivery is a harmless re-index (at-least-once + per-key ordering).
//
//   dotnet run -- "zaris://localhost:7861/mystore" "docs/"
//
// After each change it prints the current index size. Runs until Ctrl+C. Requires a running cluster
// with the change feed enabled (changeFeed.enabled=true or ZARIS_WAN_SYNC_FEED=1) and the 2.1.0+ SDK.

using System.Collections.Concurrent;
using System.Text;
using Clustron.Zaris.Abstractions.ChangeFeed;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.ChangeFeed;
using Clustron.Zaris.Client.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("ZARIS_CONNECTION_STRING")
      ?? throw new ArgumentException("Pass a connection string as arg[0] or set ZARIS_CONNECTION_STRING.");

var keyPrefix = args.Length > 1 ? args[1] : null;

const string key = "app";
var services = new ServiceCollection();
services.AddClustronZaris(key, connectionString);
using var provider = services.BuildServiceProvider();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var client = await provider.GetRequiredService<IZarisClientProvider>().GetAsync(key, cts.Token);
var feed = (IZarisChangeFeedClient)client;

var index = new InvertedIndex();

var options = new ChangeFeedOptions
{
    KeyPrefix    = keyPrefix,
    Kinds        = new[] { FeedEventKind.Put, FeedEventKind.Delete },
    ConsumerName = "search-indexer",
};

Console.WriteLine($"[cdc-index] indexing feed (prefix='{keyPrefix ?? "*"}') — Ctrl+C to stop");

try
{
    await foreach (ChangeEvent ev in feed.OpenChangeFeed(options, cts.Token))
    {
        if (ev.IsDelete)
        {
            index.Remove(ev.Key);
            Console.WriteLine($"[cdc-index] REMOVE {ev.Key}  (docs={index.DocumentCount}, terms={index.TermCount})");
        }
        else if (ev.Value is { } bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            index.Upsert(ev.Key, text);
            Console.WriteLine($"[cdc-index] INDEX  {ev.Key}  (docs={index.DocumentCount}, terms={index.TermCount})");
        }
        // non-plain values (collections/counters) are skipped by this text-indexing sample.
    }
}
catch (OperationCanceledException) { }

Console.WriteLine($"[cdc-index] stopped. docs={index.DocumentCount}, terms={index.TermCount}");
return 0;

/// <summary>A minimal term → document-keys inverted index. Idempotent: re-indexing a key first removes
/// its old postings, so a re-delivered event is a no-op net change.</summary>
sealed class InvertedIndex
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _postings = new(); // term → doc keys
    private readonly ConcurrentDictionary<string, string[]> _termsOfDoc = new();       // doc key → its terms

    public int DocumentCount => _termsOfDoc.Count;
    public int TermCount => _postings.Count;

    public void Upsert(string docKey, string text)
    {
        Remove(docKey); // drop old postings first → idempotent re-index
        var terms = Tokenize(text);
        _termsOfDoc[docKey] = terms;
        foreach (var t in terms)
            _postings.GetOrAdd(t, _ => new HashSet<string>()).Add(docKey);
    }

    public void Remove(string docKey)
    {
        if (!_termsOfDoc.TryRemove(docKey, out var terms)) return;
        foreach (var t in terms)
            if (_postings.TryGetValue(t, out var set)) { set.Remove(docKey); if (set.Count == 0) _postings.TryRemove(t, out _); }
    }

    public IReadOnlyCollection<string> Query(string term)
        => _postings.TryGetValue(term.ToLowerInvariant(), out var set) ? set : Array.Empty<string>();

    private static string[] Tokenize(string text)
        => text.ToLowerInvariant().Split(new[] { ' ', '\t', '\n', ',', '.', ';', ':', '"', '\'', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries);
}
