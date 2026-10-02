namespace FeatureFlags.Model;

/// <summary>One recorded change: who, when, what flag, which action, and the old → new summary.</summary>
public sealed class AuditEntry
{
    public long Seq { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string Actor { get; set; } = "";
    public string FlagKey { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Old { get; set; }
    public string? New { get; set; }
    /// <summary>The ruleset version produced by this change.</summary>
    public long RulesetVersion { get; set; }
}

/// <summary>
/// The append-only audit log, stored as a single CAS'd document. Appends go through compare-and-swap,
/// so when several admins commit changes concurrently no entry is ever lost — a losing appender
/// re-reads the log (now containing the other admin's entry) and appends on top, and <see cref="Seq"/>
/// stays gap-free and strictly increasing.
/// </summary>
public sealed class AuditLog
{
    public long NextSeq { get; set; } = 1;
    public List<AuditEntry> Entries { get; set; } = new();
}
