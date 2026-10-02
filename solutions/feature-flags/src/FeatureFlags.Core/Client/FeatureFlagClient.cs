using FeatureFlags.Evaluation;
using FeatureFlags.Infrastructure;
using FeatureFlags.Model;

namespace FeatureFlags.Client;

/// <summary>Options for a <see cref="FeatureFlagClient"/>.</summary>
public sealed class FeatureFlagClientOptions
{
    /// <summary>How often the client polls the ruleset document's version. The upper bound on
    /// propagation latency for a flag change (a change lands on every client within one interval).</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>A label for diagnostics / the update event (e.g. an app node id).</summary>
    public string Name { get; set; } = "client";
}

/// <summary>
/// The read side of the service — the SDK an application embeds. It keeps a cached, immutable
/// <see cref="RulesetSnapshot"/> and evaluates every flag <b>locally</b> against it, so a flag check
/// is a fast in-memory computation with no network round-trip and no per-eval latency.
///
/// <para><b>Live propagation by version-polling.</b> Native pub/sub is not exposed on the Zaris
/// client, so the SDK propagates changes by polling the ruleset document's monotonic
/// <see cref="RulesetDocument.Version"/> on a timer. When the stored version exceeds the cached
/// version, the SDK swaps in a fresh snapshot (a single atomic reference assignment) and raises
/// <see cref="Updated"/>. This is a correct, bounded-delay live-propagation mechanism: a change made
/// by an admin reaches <i>every</i> running client within one <see cref="FeatureFlagClientOptions.RefreshInterval"/>,
/// with no restart. It reads one key per poll regardless of how many flags exist.</para>
///
/// <para><b>Offline tolerance.</b> Evaluation always uses the last good snapshot. Before the first
/// load (or if the store is briefly unreachable) the snapshot is <see cref="RulesetSnapshot.Empty"/>,
/// and every evaluation returns the caller-supplied fallback — the application never blocks on the
/// store to make a decision.</para>
/// </summary>
public sealed class FeatureFlagClient : IAsyncDisposable
{
    private readonly ZarisDocumentStore _store;
    private readonly FeatureFlagClientOptions _options;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollLoop;
    private volatile RulesetSnapshot _snapshot = RulesetSnapshot.Empty;

    /// <summary>Raised on the poll loop whenever a newer ruleset version is adopted.</summary>
    public event Action<RulesetSnapshot>? Updated;

    public FeatureFlagClient(ZarisDocumentStore store, FeatureFlagClientOptions? options = null)
    {
        _store = store;
        _options = options ?? new FeatureFlagClientOptions();
    }

    /// <summary>The ruleset version the client is currently evaluating against.</summary>
    public long Version => _snapshot.Version;

    /// <summary>The current cached snapshot (immutable).</summary>
    public RulesetSnapshot Snapshot => _snapshot;

    public string Name => _options.Name;

    /// <summary>Does an initial load, then starts the background version-poll loop.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct).ConfigureAwait(false);
        _pollLoop = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Reads the ruleset once and adopts it if its version is newer than the cached one. Returns
    /// <c>true</c> if a newer version was adopted. Exposed so tests/apps can force a refresh instead
    /// of waiting for the timer.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<RulesetDocument>(Keys.Ruleset, ct).ConfigureAwait(false);
        if (!doc.Found || doc.Value is null)
            return false;
        if (doc.Value.Version <= _snapshot.Version)
            return false;

        var snapshot = RulesetSnapshot.From(doc.Value);
        _snapshot = snapshot;                 // atomic reference swap; readers see old or new, never torn.
        Updated?.Invoke(snapshot);
        return true;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RefreshInterval, ct).ConfigureAwait(false);
                await RefreshAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // Transient store error: keep the last good snapshot and try again next tick.
            }
        }
    }

    // ---- local evaluation (no I/O) ----

    /// <summary>Full evaluation detail (variation, reason, ruleset version) for one flag + user.</summary>
    public EvalDetail Evaluate(string flagKey, UserContext user)
        => FlagEvaluator.Evaluate(_snapshot, flagKey, user);

    /// <summary>Evaluates a boolean flag, returning <paramref name="fallback"/> if absent/unparseable.</summary>
    public bool BoolVariation(string flagKey, UserContext user, bool fallback = false)
    {
        var d = Evaluate(flagKey, user);
        return d.Value is not null && bool.TryParse(d.Value, out var b) ? b : fallback;
    }

    /// <summary>Evaluates a multivariate string flag, returning <paramref name="fallback"/> if absent.</summary>
    public string StringVariation(string flagKey, UserContext user, string fallback = "")
    {
        var d = Evaluate(flagKey, user);
        return d.Value ?? fallback;
    }

    /// <summary>Evaluates a numeric flag, returning <paramref name="fallback"/> if absent/unparseable.</summary>
    public double NumberVariation(string flagKey, UserContext user, double fallback = 0)
    {
        var d = Evaluate(flagKey, user);
        return d.Value is not null &&
               double.TryParse(d.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n : fallback;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_pollLoop is not null)
        {
            try { await _pollLoop.ConfigureAwait(false); } catch { /* loop cancelled */ }
        }
        _cts.Dispose();
    }
}
