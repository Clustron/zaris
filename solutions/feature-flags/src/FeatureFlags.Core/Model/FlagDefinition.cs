namespace FeatureFlags.Model;

/// <summary>The value-type a flag serves. Variation values are stored as strings and parsed per kind.</summary>
public enum FlagKind
{
    /// <summary>Two variations, parsed as <see cref="bool"/> (e.g. "true"/"false").</summary>
    Bool,
    /// <summary>Any number of string variations (multivariate), e.g. "control"/"blue"/"green".</summary>
    String,
    /// <summary>Any number of numeric variations, parsed as <see cref="double"/>.</summary>
    Number
}

/// <summary>How a matched rule (or the fallthrough) resolves to a variation.</summary>
public enum ClauseOp
{
    /// <summary>Attribute equals any of the clause values (case-sensitive string equality).</summary>
    In,
    /// <summary>Attribute equals none of the clause values.</summary>
    NotIn,
    /// <summary>Attribute string contains any of the clause values.</summary>
    Contains,
    /// <summary>Attribute string starts with any of the clause values.</summary>
    StartsWith,
    /// <summary>Numeric attribute &gt; the (single) clause value.</summary>
    GreaterThan,
    /// <summary>Numeric attribute &lt; the (single) clause value.</summary>
    LessThan,
    /// <summary>Attribute matches the (single) clause value as a .NET regex.</summary>
    Matches
}

/// <summary>A single attribute test. All clauses in a rule are AND'd together.</summary>
public sealed class Clause
{
    public string Attribute { get; set; } = "";
    public ClauseOp Op { get; set; } = ClauseOp.In;
    public List<string> Values { get; set; } = new();

    public Clause() { }
    public Clause(string attribute, ClauseOp op, params string[] values)
    {
        Attribute = attribute;
        Op = op;
        Values = values.ToList();
    }
}

/// <summary>One weighted slice of a percentage rollout: a variation and its parts-per-100000 weight.</summary>
public sealed class RolloutBucket
{
    public int VariationIndex { get; set; }
    /// <summary>Weight in parts-per-100,000 (so 20% = 20000). Buckets in a rollout sum to 100000.</summary>
    public int Weight { get; set; }

    public RolloutBucket() { }
    public RolloutBucket(int variationIndex, int weight)
    {
        VariationIndex = variationIndex;
        Weight = weight;
    }
}

/// <summary>
/// A deterministic, sticky percentage rollout. The ordered <see cref="Buckets"/> partition the
/// [0,1) hash space; a user's bucket value (see <c>Bucketing</c>) selects the first bucket whose
/// cumulative weight it falls under. <see cref="Salt"/> lets you reshuffle the assignment without
/// touching user keys. Because the bucket value depends only on (flag key + salt + user key), the
/// assignment is identical on every client and across evaluations — no server round-trip.
/// </summary>
public sealed class Rollout
{
    public List<RolloutBucket> Buckets { get; set; } = new();
    /// <summary>Optional salt; defaults to the flag key when empty. Change it to reshuffle buckets.</summary>
    public string Salt { get; set; } = "";
    /// <summary>Attribute whose value is hashed instead of the user key (defaults to the user key).</summary>
    public string BucketBy { get; set; } = "key";

    public Rollout() { }

    /// <summary>
    /// A simple two-way rollout: <paramref name="percent"/>% land on <paramref name="onVariation"/>,
    /// the rest on <paramref name="offVariation"/>. The "on" bucket is kept <b>first</b> so that
    /// increasing the percentage only ever <i>adds</i> users to it — nobody already rolled-in ever
    /// drops out (monotonic, sticky growth).
    /// </summary>
    public static Rollout Percentage(int onVariation, int offVariation, double percent, string salt = "")
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        var on = (int)Math.Round(percent * 1000); // percent of 100000
        return new Rollout
        {
            Salt = salt,
            Buckets = new List<RolloutBucket>
            {
                new(onVariation, on),
                new(offVariation, 100000 - on)
            }
        };
    }
}

/// <summary>
/// A targeting rule: if every <see cref="Clauses"/> matches, the rule serves either a fixed
/// <see cref="VariationIndex"/> or (if set) a <see cref="Rollout"/>. Rules are evaluated in order;
/// the first match wins.
/// </summary>
public sealed class TargetingRule
{
    public List<Clause> Clauses { get; set; } = new();
    public int VariationIndex { get; set; }
    public Rollout? Rollout { get; set; }

    public TargetingRule() { }
    public TargetingRule(int variationIndex, params Clause[] clauses)
    {
        VariationIndex = variationIndex;
        Clauses = clauses.ToList();
    }
}

/// <summary>Direct per-user targeting: these user keys always get <see cref="VariationIndex"/>.</summary>
public sealed class Target
{
    public int VariationIndex { get; set; }
    public List<string> UserKeys { get; set; } = new();

    public Target() { }
    public Target(int variationIndex, params string[] userKeys)
    {
        VariationIndex = variationIndex;
        UserKeys = userKeys.ToList();
    }
}

/// <summary>A prerequisite flag that must serve a required variation, or this flag serves Off.</summary>
public sealed class Prerequisite
{
    public string FlagKey { get; set; } = "";
    public int RequiredVariationIndex { get; set; }

    public Prerequisite() { }
    public Prerequisite(string flagKey, int requiredVariationIndex)
    {
        FlagKey = flagKey;
        RequiredVariationIndex = requiredVariationIndex;
    }
}

/// <summary>
/// A complete flag definition — a self-contained, serializable spec that any client can evaluate
/// locally. The evaluation order (LaunchDarkly-style) is:
/// Off (kill switch) → prerequisites → explicit targets → rules (first match) → fallthrough.
/// </summary>
public sealed class FlagDefinition
{
    public string Key { get; set; } = "";
    public FlagKind Kind { get; set; } = FlagKind.Bool;

    /// <summary>The possible values, as strings (parsed per <see cref="Kind"/>). Indices are referenced everywhere.</summary>
    public List<string> Variations { get; set; } = new();

    /// <summary>Master on/off. When off, the flag always serves <see cref="OffVariationIndex"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Variation served when the flag is off or a prerequisite fails.</summary>
    public int OffVariationIndex { get; set; }

    /// <summary>Flags that must be on and serving a required variation, else this flag serves Off.</summary>
    public List<Prerequisite> Prerequisites { get; set; } = new();

    /// <summary>Direct per-user overrides, checked before rules.</summary>
    public List<Target> Targets { get; set; } = new();

    /// <summary>Ordered targeting rules; first match wins.</summary>
    public List<TargetingRule> Rules { get; set; } = new();

    /// <summary>What an enabled flag serves when nothing above matched: a fixed variation…</summary>
    public int FallthroughVariationIndex { get; set; }
    /// <summary>…or a percentage rollout (takes precedence over the fixed fallthrough when set).</summary>
    public Rollout? FallthroughRollout { get; set; }

    // ---- convenience constructors for the common shapes ----

    /// <summary>A boolean flag. Variation 0 = true, 1 = false. Off and fallthrough default to false.</summary>
    public static FlagDefinition Boolean(string key, bool enabled = true, bool fallthrough = false)
        => new()
        {
            Key = key,
            Kind = FlagKind.Bool,
            Variations = new List<string> { "true", "false" },
            Enabled = enabled,
            OffVariationIndex = 1,                        // false when off
            FallthroughVariationIndex = fallthrough ? 0 : 1
        };

    /// <summary>A multivariate string flag. The first variation is the default off/fallthrough value.</summary>
    public static FlagDefinition Multivariate(string key, params string[] variations)
        => new()
        {
            Key = key,
            Kind = FlagKind.String,
            Variations = variations.ToList(),
            Enabled = true,
            OffVariationIndex = 0,
            FallthroughVariationIndex = 0
        };

    /// <summary>A numeric flag. The first variation is the default off/fallthrough value.</summary>
    public static FlagDefinition Numeric(string key, params double[] variations)
        => new()
        {
            Key = key,
            Kind = FlagKind.Number,
            Variations = variations.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList(),
            Enabled = true,
            OffVariationIndex = 0,
            FallthroughVariationIndex = 0
        };

    /// <summary>A deep copy, so an admin mutation never aliases the previously-committed document.</summary>
    public FlagDefinition Clone()
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this, Infrastructure.ZarisDocumentStore.Json);
        return System.Text.Json.JsonSerializer.Deserialize<FlagDefinition>(bytes, Infrastructure.ZarisDocumentStore.Json)!;
    }
}
