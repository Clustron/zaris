namespace FeatureFlags;

/// <summary>
/// Every key the service stores. There are exactly two: the ruleset document and the audit log.
/// Zaris has no prefix scan, so the service never needs one — the ruleset document <i>is</i> the
/// index of all flags (a client reads one key to get every flag), and the audit log is a single
/// append-only document. Both are CAS'd.
/// </summary>
public static class Keys
{
    /// <summary>The single versioned ruleset document: all flag definitions + a monotonic version.</summary>
    public const string Ruleset = "fflags:ruleset";

    /// <summary>The single append-only audit log document.</summary>
    public const string Audit = "fflags:audit";
}
