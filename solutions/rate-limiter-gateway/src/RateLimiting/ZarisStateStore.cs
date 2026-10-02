using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace RateLimiterGateway.RateLimiting;

/// <summary>
/// Thin wrapper over <see cref="IZarisClient"/> that turns Zaris's whole-value read / versioned
/// compare-and-swap write into the primitive the limiters need:
/// <list type="bullet">
///   <item>Read the current state together with its monotonic <c>Version</c>.</item>
///   <item>Write a new state only if the version is unchanged (optimistic CAS), or only if the key
///         is still absent (create). A losing writer gets <see cref="KvStatus.Conflict"/> back and the
///         caller retries the read-modify-write — this is what makes the counters race-free across
///         concurrent requests and across multiple gateway instances sharing the store.</item>
/// </list>
/// State is serialized as compact UTF-8 JSON and stored as a <c>byte[]</c> value.
/// </summary>
public sealed class ZarisStateStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IZarisClient _client;

    public ZarisStateStore(IZarisClient client) => _client = client;

    public readonly record struct Snapshot<T>(T? Value, long Version, bool Found);

    /// <summary>Read current state and its CAS version. <c>Found=false</c> when the key is absent.</summary>
    public async Task<Snapshot<T>> ReadAsync<T>(string key, CancellationToken ct)
    {
        var r = await _client.GetAsync<byte[]>(key, ct: ct).ConfigureAwait(false);
        if (r.IsSuccess && r.Value is { Length: > 0 })
        {
            var value = JsonSerializer.Deserialize<T>(r.Value, Json);
            return new Snapshot<T>(value, r.Version ?? 0, true);
        }
        if (r.Status is KvStatus.Success or KvStatus.NotFound)
            return new Snapshot<T>(default, 0, false);

        // NotFound/Success-empty are normal "no state yet"; anything else is a real error.
        throw new ZarisRateLimiterException($"Zaris GET '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Create the key only if it is still absent. Returns <c>true</c> if this caller created it,
    /// <c>false</c> if someone else created it first (lost the race).
    /// </summary>
    public async Task<bool> CreateIfAbsentAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var r = await _client.PutAsync<byte[]>(key, bytes, new PutOptions { IfAbsent = true }, ct).ConfigureAwait(false);
        if (r.IsSuccess)
        {
            await SetTtlBestEffortAsync(key, ttl, ct).ConfigureAwait(false);
            return true;
        }
        if (r.Status == KvStatus.Conflict) return false;
        throw new ZarisRateLimiterException($"Zaris PUT(ifAbsent) '{key}' failed: {r.Status} {r.Error}");
    }

    /// <summary>
    /// Replace the value only if its version is still <paramref name="expectedVersion"/> (CAS).
    /// Returns <c>true</c> on commit, <c>false</c> if another writer won (version moved) — caller retries.
    /// </summary>
    public async Task<bool> CompareAndSwapAsync<T>(string key, long expectedVersion, T value, TimeSpan? ttl, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        var opt = new PutOptions { IfMatchVersion = new ItemVersion(expectedVersion) };
        var r = await _client.PutAsync<byte[]>(key, bytes, opt, ct).ConfigureAwait(false);
        if (r.IsSuccess)
        {
            await SetTtlBestEffortAsync(key, ttl, ct).ConfigureAwait(false);
            return true;
        }
        if (r.Status == KvStatus.Conflict) return false;
        throw new ZarisRateLimiterException($"Zaris PUT(ifMatch={expectedVersion}) '{key}' failed: {r.Status} {r.Error}");
    }

    private async Task SetTtlBestEffortAsync(string key, TimeSpan? ttl, CancellationToken ct)
    {
        if (ttl is null || ttl.Value <= TimeSpan.Zero) return;
        // TTL is only idle-key garbage collection; correctness never depends on server-side eviction
        // (every decision recomputes freshness from stored state + the current clock). Best-effort.
        try { await _client.ExpireAsync(key, ttl.Value, ct).ConfigureAwait(false); }
        catch { /* ignore: GC hint only */ }
    }
}
