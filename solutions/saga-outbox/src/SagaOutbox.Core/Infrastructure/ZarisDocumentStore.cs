using System.Text.Json;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace SagaOutbox.Infrastructure;

/// <summary>
/// The single storage primitive the whole solution is built on: a document store with
/// <b>optimistic, versioned compare-and-swap</b> on top of <see cref="IZarisClient"/>.
///
/// Zaris has no server-side atomic increment or multi-key transaction we rely on here; instead every
/// read returns the value together with its monotonic <see cref="KvResult.Version"/>, and a write can
/// be made conditional on that version (<see cref="PutOptions.IfMatchVersion"/>) or on the key still
/// being absent (<see cref="PutOptions.IfAbsent"/>). A losing writer gets <see cref="KvStatus.Conflict"/>
/// and retries the whole read-modify-write. That is what makes a <b>single document</b> an atomic unit:
/// business state, an idempotency ledger and an outbox can all live in one document and be committed
/// together in one CAS — which is exactly how this solution avoids the dual-write problem.
///
/// Documents are serialized as compact UTF-8 JSON and stored as a <c>byte[]</c> value.
/// </summary>
public sealed class ZarisDocumentStore
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        // Deterministic, compact; records serialize cleanly.
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

        throw new ZarisStoreException($"Zaris GET '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Creates the key only if it is still absent. Returns <c>true</c> if this caller created it,
    /// <c>false</c> if someone else created it first (lost the create race).
    /// </summary>
    public async Task<bool> CreateAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var r = await _client.PutAsync<byte[]>(key, bytes, new PutOptions { IfAbsent = true }, ct).ConfigureAwait(false);
        if (r.IsSuccess) return true;
        if (r.Status == KvStatus.Conflict) return false;
        throw new ZarisStoreException($"Zaris PUT(ifAbsent) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Replaces the value only if its version is still <paramref name="expectedVersion"/> (CAS).
    /// Returns <c>true</c> on commit, <c>false</c> if another writer won (version moved) — caller retries.
    /// </summary>
    public async Task<bool> CompareAndSwapAsync<T>(string key, long expectedVersion, T value, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var opt = new PutOptions { IfMatchVersion = new ItemVersion(expectedVersion) };
        var r = await _client.PutAsync<byte[]>(key, bytes, opt, ct).ConfigureAwait(false);
        if (r.IsSuccess) return true;
        if (r.Status == KvStatus.Conflict) return false;
        throw new ZarisStoreException($"Zaris PUT(ifMatch={expectedVersion}) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// The workhorse: an atomic read-modify-write on one document with bounded CAS retry.
    ///
    /// <paramref name="mutate"/> receives the current state (or <paramref name="create"/>'s result when
    /// the key is absent) and returns the next state to commit, or <c>null</c> to indicate "no change
    /// needed" (used for idempotent no-ops — e.g. a command that was already applied). On a
    /// <see cref="KvStatus.Conflict"/> the loop re-reads the latest state and runs <paramref name="mutate"/>
    /// again, so the decision is always made against committed state. Returns the state that is in the
    /// store when the method returns (the committed one, or the unchanged current one for a no-op).
    /// </summary>
    public async Task<T> MutateAsync<T>(
        string key,
        Func<T, T?> mutate,
        Func<T> create,
        int maxAttempts = 64,
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
                // Try to create; if someone created it first, retry against their version.
                if (await CreateAsync(key, next, ct).ConfigureAwait(false))
                    return next;
                continue;
            }

            var proposed = mutate(current.Value!);
            if (proposed is null)
                return current.Value!; // idempotent no-op; nothing to write.

            if (await CompareAndSwapAsync(key, current.Version, proposed, ct).ConfigureAwait(false))
                return proposed;
            // lost the race — loop and retry against the new committed state.
        }
        throw new ZarisStoreException($"CAS retry budget exhausted for '{key}' after {maxAttempts} attempts (contention).");
    }
}

/// <summary>Thrown when the backing store returns a terminal (non-retryable) failure.</summary>
public sealed class ZarisStoreException : Exception
{
    public ZarisStoreException(string message) : base(message) { }
}
