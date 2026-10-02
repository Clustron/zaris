namespace FeatureFlags.Model;

/// <summary>
/// The single document every client polls. It carries a monotonically increasing
/// <see cref="Version"/> that is bumped on <b>every</b> admin change — that domain version is the
/// cheap "has anything changed?" signal clients compare against their cached copy. (It is distinct
/// from the Zaris CAS version, which the admin uses under the hood to avoid lost updates.)
/// </summary>
public sealed class RulesetDocument
{
    /// <summary>Monotonic ruleset version; bumped on every committed change. Clients refresh when it grows.</summary>
    public long Version { get; set; }

    /// <summary>All flags, keyed by flag key. The ruleset document is the index of flags (no prefix scan needed).</summary>
    public Dictionary<string, FlagDefinition> Flags { get; set; } = new();

    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = "";

    /// <summary>A deep copy so an admin mutation works on a private copy, never aliasing committed state.</summary>
    public RulesetDocument Clone()
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this, Infrastructure.ZarisDocumentStore.Json);
        return System.Text.Json.JsonSerializer.Deserialize<RulesetDocument>(bytes, Infrastructure.ZarisDocumentStore.Json)!;
    }
}

/// <summary>
/// An immutable, point-in-time view of the ruleset held by a client. Evaluation reads only this —
/// never the store — so a flag check is a fast, allocation-light, offline-tolerant local computation.
/// </summary>
public sealed class RulesetSnapshot
{
    public long Version { get; }
    public IReadOnlyDictionary<string, FlagDefinition> Flags { get; }

    public RulesetSnapshot(long version, IReadOnlyDictionary<string, FlagDefinition> flags)
    {
        Version = version;
        Flags = flags;
    }

    /// <summary>An empty snapshot (version 0) — what a client evaluates against before its first load.</summary>
    public static readonly RulesetSnapshot Empty = new(0, new Dictionary<string, FlagDefinition>());

    public static RulesetSnapshot From(RulesetDocument doc) => new(doc.Version, doc.Flags);
}
