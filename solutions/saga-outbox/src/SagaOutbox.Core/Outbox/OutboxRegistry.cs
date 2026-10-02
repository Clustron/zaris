using SagaOutbox.Infrastructure;

namespace SagaOutbox.Outbox;

/// <summary>Stored shape of a service's registry: the set of aggregate document keys it owns.</summary>
public sealed class RegistrySet
{
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// Discovery metadata for the relay. Because the Zaris client surface used here is Put/Get/Delete +
/// CAS only (no key-prefix scan), the relay cannot enumerate an aggregate keyspace on its own; each
/// service instead records its aggregate document keys in a small registry set when an aggregate is
/// first created, and the relay polls those keys.
///
/// Note this is only <i>discovery</i> metadata — the events themselves always live inside the
/// aggregate document, co-committed with state. A missed registry entry can only <i>delay</i> a
/// relay, never lose an event; a production deployment would replace this with a prefix scan.
/// </summary>
public sealed class OutboxRegistry
{
    private readonly ZarisDocumentStore _store;
    private readonly string _key;

    public OutboxRegistry(ZarisDocumentStore store, string service)
    {
        _store = store;
        _key = $"outbox:registry:{service}";
    }

    /// <summary>Idempotently records that <paramref name="aggregateKey"/> exists and may have outbox events.</summary>
    public Task RegisterAsync(string aggregateKey, CancellationToken ct = default)
        => _store.MutateAsync<RegistrySet>(
            _key,
            set => set.Keys.Contains(aggregateKey) ? null : Add(set, aggregateKey),
            create: () => new RegistrySet(),
            ct: ct);

    private static RegistrySet Add(RegistrySet set, string key)
    {
        set.Keys.Add(key);
        return set;
    }

    public async Task<IReadOnlyList<string>> KeysAsync(CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<RegistrySet>(_key, ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.Keys.ToList() : Array.Empty<string>();
    }
}
