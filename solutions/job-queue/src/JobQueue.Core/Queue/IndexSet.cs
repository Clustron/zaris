using JobQueue.Infrastructure;

namespace JobQueue.Queue;

/// <summary>The stored shape of an index: a plain set of job ids kept insertion-ordered.</summary>
public sealed class IndexDoc
{
    public List<string> Ids { get; set; } = new();
}

/// <summary>
/// A membership set stored as a single CAS'd Zaris document — the queue's answer to "there is no
/// prefix scan". Enqueue adds an id; a terminal transition removes it. The hot claim path never writes
/// the index (a claim only CASes the job document), so workers contend per-job, not on a global lock;
/// the index is touched only on enqueue and on completion/dead-lettering.
///
/// Add/Remove are idempotent (safe to replay), which is what lets a retried enqueue heal a
/// half-finished index without creating duplicates.
/// </summary>
public sealed class IndexSet
{
    private readonly ZarisDocumentStore _store;
    private readonly string _key;

    public IndexSet(ZarisDocumentStore store, string key)
    {
        _store = store;
        _key = key;
    }

    public Task AddAsync(string id, CancellationToken ct = default) =>
        _store.MutateAsync<IndexDoc>(
            _key,
            doc =>
            {
                if (doc.Ids.Contains(id)) return null; // already present — idempotent no-op.
                doc.Ids.Add(id);
                return doc;
            },
            create: () => new IndexDoc(),
            ct: ct);

    public Task RemoveAsync(string id, CancellationToken ct = default) =>
        _store.MutateAsync<IndexDoc>(
            _key,
            doc => doc.Ids.Remove(id) ? doc : null, // not present — idempotent no-op.
            create: () => new IndexDoc(),
            ct: ct);

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<IndexDoc>(_key, ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.Ids.ToList() : Array.Empty<string>();
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<IndexDoc>(_key, ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.Ids.Count : 0;
    }
}
