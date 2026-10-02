using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace LiveBetting.Core.Infrastructure;

/// <summary>
/// The one storage primitive the whole engine is built on: a JSON document store with
/// <b>optimistic, versioned compare-and-swap</b> over <see cref="IZaris"/>.
///
/// Zaris has no server-side atomic increment and no multi-key transaction we lean on. Instead every
/// read returns the value together with its monotonic <see cref="KvResult.Version"/>, and a write can
/// be made conditional on that version (<see cref="PutOptions.IfMatchVersion"/>) or on the key still
/// being absent (<see cref="PutOptions.IfAbsent"/>). A losing writer gets <see cref="KvStatus.Conflict"/>
/// and retries the whole read-modify-write. That turns a <b>single document</b> into an atomic unit —
/// which is exactly how a wallet balance is debited race-free under thousands of concurrent bets.
///
/// A write-time TTL can be attached via <see cref="PutOptions.Metadata"/> so short-lived records
/// (e.g. quote/requote markers) self-reclaim; TTL is always set at write time, never through a
/// separate expire call.
/// </summary>
public sealed class DocumentStore
{
    private readonly IZaris _z;

    public DocumentStore(IZaris zaris) => _z = zaris;

    /// <summary>A document read together with the CAS version it was read at.</summary>
    public readonly record struct Doc<T>(T? Value, long Version, bool Found);

    /// <summary>Reads a document and its CAS version. <c>Found=false</c> when the key is absent.</summary>
    public async Task<Doc<T>> ReadAsync<T>(string key, CancellationToken ct = default)
    {
        var r = await _z.GetAsync<byte[]>(key, null, ct).ConfigureAwait(false);
        if (r.IsSuccess && r.Value is { Length: > 0 })
        {
            var value = Json.From<T>(r.Value);
            return new Doc<T>(value, r.Version?.Version ?? 0, true);
        }
        if (r.Status is KvStatus.Success or KvStatus.NotFound)
            return new Doc<T>(default, 0, false);

        throw new BettingStoreException($"Zaris GET '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Creates the key only if it is still absent. Returns <c>true</c> if this caller created it,
    /// <c>false</c> if someone else created it first (lost the create race — the basis of idempotent,
    /// create-once records such as a settlement-log entry).
    /// </summary>
    public async Task<bool> CreateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var r = await _z.PutAsync<byte[]>(key, Json.Bytes(value), NewOptions(ifAbsent: true, ttl: ttl), ct)
            .ConfigureAwait(false);
        if (r.IsSuccess) return true;
        if (r.Status == KvStatus.Conflict) return false;
        throw new BettingStoreException($"Zaris PUT(ifAbsent) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Replaces the value only if its version is still <paramref name="expectedVersion"/> (CAS).
    /// Returns <c>true</c> on commit, <c>false</c> if another writer won (version moved) — caller retries.
    /// </summary>
    public async Task<bool> CompareAndSwapAsync<T>(string key, long expectedVersion, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var r = await _z.PutAsync<byte[]>(key, Json.Bytes(value), NewOptions(ifMatch: expectedVersion, ttl: ttl), ct)
            .ConfigureAwait(false);
        if (r.IsSuccess) return true;
        if (r.Status == KvStatus.Conflict) return false;
        throw new BettingStoreException($"Zaris PUT(ifMatch={expectedVersion}) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// The workhorse: an atomic read-modify-write on one document with bounded CAS retry.
    ///
    /// <paramref name="mutate"/> receives the current state (or <paramref name="create"/>'s result when the
    /// key is absent) and returns the next state to commit, or <c>null</c> for "no change needed" (an
    /// idempotent no-op — e.g. a transaction that was already applied). On <see cref="KvStatus.Conflict"/>
    /// the loop re-reads the latest committed state and runs <paramref name="mutate"/> again, so the
    /// decision is always made against committed state. Returns the state in the store when it returns.
    /// </summary>
    public async Task<T> MutateAsync<T>(
        string key,
        Func<T, T?> mutate,
        Func<T> create,
        int maxAttempts = 128,
        CancellationToken ct = default)
        where T : class
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var current = await ReadAsync<T>(key, ct).ConfigureAwait(false);

            if (!current.Found)
            {
                var initial = create();
                var next = mutate(initial) ?? initial;
                if (await CreateAsync(key, next, null, ct).ConfigureAwait(false))
                    return next;
                continue; // someone created it first — retry against their version.
            }

            var proposed = mutate(current.Value!);
            if (proposed is null)
                return current.Value!; // idempotent no-op; nothing to write.

            if (await CompareAndSwapAsync(key, current.Version, proposed, null, ct).ConfigureAwait(false))
                return proposed;
            // lost the race — loop and retry against the new committed state.
        }
        throw new BettingStoreException($"CAS retry budget exhausted for '{key}' after {maxAttempts} attempts (contention).");
    }

    private static PutOptions NewOptions(bool ifAbsent = false, long? ifMatch = null, TimeSpan? ttl = null)
    {
        var o = new PutOptions { IfAbsent = ifAbsent };
        if (ifMatch is { } v) o.IfMatchVersion = new ItemVersion(v);
        if (ttl is { } t) o.Metadata = new EntryMetadata { Ttl = t };
        return o;
    }
}

/// <summary>Thrown when the backing store returns a terminal (non-retryable) failure.</summary>
public sealed class BettingStoreException : Exception
{
    public BettingStoreException(string message) : base(message) { }
}
