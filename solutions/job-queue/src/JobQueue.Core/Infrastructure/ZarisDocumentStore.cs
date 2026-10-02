using System.Text.Json;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace JobQueue.Infrastructure;

/// <summary>
/// The one storage primitive the whole queue is built on: a JSON document store with
/// <b>optimistic, versioned compare-and-swap</b> over <see cref="IZarisClient"/>, plus an optional
/// write-time TTL.
///
/// Zaris has no server-side atomic increment and no multi-key transaction we rely on here. Instead
/// every read returns the value together with its monotonic <see cref="KvResult.Version"/>, and a
/// write can be made conditional on that version (<see cref="PutOptions.IfMatchVersion"/>) or on the
/// key still being absent (<see cref="PutOptions.IfAbsent"/>). A losing writer gets
/// <see cref="KvStatus.Conflict"/> and retries the whole read-modify-write. That turns a
/// <b>single document</b> into an atomic unit — which is exactly how a job is claimed by at most one
/// worker: the Ready→Claimed flip is one CAS, and the monotonic version makes concurrent claimers
/// serialise so only one wins.
///
/// A write-time TTL can be attached via <see cref="PutOptions.Metadata"/> so short-lived records
/// (the in-flight lease marker, finished-job receipts) self-reclaim. TTL is always set at write time,
/// never through a separate expire call (a known InProc <c>ExpireAsync</c> bug makes the latter
/// unreliable).
///
/// Documents are serialized as compact UTF-8 JSON and stored as a <c>byte[]</c> value.
/// </summary>
public sealed class ZarisDocumentStore
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IZarisClient _client;

    public ZarisDocumentStore(IZarisClient client) => _client = client;

    /// <summary>A document read together with the CAS version it was read at.</summary>
    public readonly record struct Doc<T>(T? Value, long Version, bool Found);

    /// <summary>Reads a document and its CAS version. <c>Found=false</c> when the key is absent.</summary>
    public async Task<Doc<T>> ReadAsync<T>(string key, CancellationToken ct = default)
    {
        var r = await _client.GetAsync<byte[]>(key, ct: ct).ConfigureAwait(false);
        if (r.IsSuccess && r.Value is { Length: > 0 })
        {
            var value = JsonSerializer.Deserialize<T>(r.Value, Json);
            return new Doc<T>(value, r.Version?.Version ?? 0, true);
        }
        if (r.Status is KvStatus.Success or KvStatus.NotFound)
            return new Doc<T>(default, 0, false);

        throw new JobStoreException($"Zaris GET '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Creates the key only if it is still absent. Returns <c>true</c> if this caller created it,
    /// <c>false</c> if someone else created it first (lost the create race). An optional write-time
    /// TTL lets the created key self-reclaim.
    /// </summary>
    public async Task<bool> CreateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var r = await _client.PutAsync<byte[]>(key, bytes, NewOptions(ifAbsent: true, ttl: ttl), ct).ConfigureAwait(false);
        if (r.IsSuccess) return true;
        if (r.Status == KvStatus.Conflict) return false;
        throw new JobStoreException($"Zaris PUT(ifAbsent) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Replaces the value only if its version is still <paramref name="expectedVersion"/> (CAS).
    /// Returns <c>true</c> on commit, <c>false</c> if another writer won (version moved) — caller retries.
    /// An optional write-time TTL lets the written value self-reclaim.
    /// </summary>
    public async Task<bool> CompareAndSwapAsync<T>(
        string key, long expectedVersion, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var r = await _client.PutAsync<byte[]>(key, bytes, NewOptions(ifMatch: expectedVersion, ttl: ttl), ct)
            .ConfigureAwait(false);
        if (r.IsSuccess) return true;
        if (r.Status == KvStatus.Conflict) return false;
        throw new JobStoreException($"Zaris PUT(ifMatch={expectedVersion}) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>Unconditionally writes a value (optionally with a write-time TTL).</summary>
    public async Task PutAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var r = await _client.PutAsync<byte[]>(key, bytes, NewOptions(ttl: ttl), ct).ConfigureAwait(false);
        if (!r.IsSuccess)
            throw new JobStoreException($"Zaris PUT '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>Deletes a key (best effort; absent is fine).</summary>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteAsync(key, null, ct).ConfigureAwait(false);
    }

    /// <summary>True if the key currently exists in the store (used to observe TTL reclamation).</summary>
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var r = await _client.GetAsync<byte[]>(key, ct: ct).ConfigureAwait(false);
        return r.IsSuccess && r.Value is { Length: > 0 };
    }

    /// <summary>
    /// An atomic read-modify-write on one document with bounded CAS retry. <paramref name="mutate"/>
    /// receives the current state (or <paramref name="create"/>'s result when the key is absent) and
    /// returns the next state to commit, or <c>null</c> for an idempotent no-op. On a conflict the loop
    /// re-reads committed state and runs <paramref name="mutate"/> again, so the decision is always made
    /// against the latest committed value.
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
                if (await CreateAsync(key, next, ct: ct).ConfigureAwait(false))
                    return next;
                continue; // someone created it first — retry against their version.
            }

            var proposed = mutate(current.Value!);
            if (proposed is null)
                return current.Value!; // idempotent no-op; nothing to write.

            if (await CompareAndSwapAsync(key, current.Version, proposed, ct: ct).ConfigureAwait(false))
                return proposed;
            // lost the race — loop and retry against the new committed state.
        }
        throw new JobStoreException($"CAS retry budget exhausted for '{key}' after {maxAttempts} attempts (contention).");
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
public sealed class JobStoreException : Exception
{
    public JobStoreException(string message) : base(message) { }
}
