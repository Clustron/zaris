namespace FeatureFlags.Model;

/// <summary>Why a flag resolved to the variation it did — useful for diagnostics and audit.</summary>
public enum EvalReason
{
    /// <summary>Flag's master switch is off; served the off variation.</summary>
    Off,
    /// <summary>A prerequisite flag was off or served the wrong variation; served the off variation.</summary>
    PrerequisiteFailed,
    /// <summary>The user key was in an explicit target list.</summary>
    TargetMatch,
    /// <summary>A targeting rule's clauses all matched.</summary>
    RuleMatch,
    /// <summary>Nothing above matched; served the fallthrough (fixed or rollout).</summary>
    Fallthrough,
    /// <summary>Requested flag is not in the ruleset; the caller's fallback was returned.</summary>
    FlagNotFound,
    /// <summary>A malformed flag (bad variation index etc.); the caller's fallback was returned.</summary>
    Error
}

/// <summary>The full result of evaluating one flag for one user.</summary>
public sealed class EvalDetail
{
    public string FlagKey { get; init; } = "";
    /// <summary>The chosen variation's raw string value (parse per flag kind), or null when not found.</summary>
    public string? Value { get; init; }
    /// <summary>Index into the flag's variation list, or -1 when not found/error.</summary>
    public int VariationIndex { get; init; }
    public EvalReason Reason { get; init; }
    /// <summary>The ruleset version this evaluation was made against (the snapshot's version).</summary>
    public long RulesetVersion { get; init; }

    public bool IsDefaulted => Reason is EvalReason.FlagNotFound or EvalReason.Error;
}
