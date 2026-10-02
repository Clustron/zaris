using FeatureFlags.Infrastructure;
using FeatureFlags.Model;

namespace FeatureFlags.Admin;

/// <summary>
/// The write side of the service. Every change is applied as an <b>optimistic CAS read-modify-write</b>
/// on the single ruleset document: read the document and its Zaris version, apply the edit to a private
/// copy, bump the domain <see cref="RulesetDocument.Version"/>, and compare-and-swap. If a concurrent
/// admin committed first the CAS fails, and this call re-reads <i>their</i> committed state and
/// re-applies on top — so concurrent edits never lose each other's work. After the ruleset commit, the
/// change is appended to the audit log, itself a CAS'd append so audit entries are never lost either.
/// </summary>
public sealed class FeatureFlagAdmin
{
    private readonly ZarisDocumentStore _store;
    private readonly Func<DateTimeOffset> _now;

    public FeatureFlagAdmin(ZarisDocumentStore store, Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _now = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Captured old → new summary for one change, produced inside the CAS closure.</summary>
    private readonly record struct AuditDraft(string FlagKey, string Action, string? Old, string? New);

    /// <summary>Reads the current ruleset (empty document if none written yet).</summary>
    public async Task<RulesetDocument> GetRulesetAsync(CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<RulesetDocument>(Keys.Ruleset, ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value : new RulesetDocument();
    }

    /// <summary>Reads the full audit log, oldest first.</summary>
    public async Task<IReadOnlyList<AuditEntry>> GetAuditAsync(CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<AuditLog>(Keys.Audit, ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.Entries : Array.Empty<AuditEntry>();
    }

    // ---- public mutations ----

    /// <summary>Creates or replaces a flag definition. Returns the new ruleset version.</summary>
    public Task<long> DefineFlagAsync(FlagDefinition flag, string actor, CancellationToken ct = default)
        => ApplyAsync(actor, rs =>
        {
            var existed = rs.Flags.TryGetValue(flag.Key, out var old);
            rs.Flags[flag.Key] = flag.Clone();
            return new AuditDraft(flag.Key, existed ? "redefine" : "define",
                existed ? Summarize(old!) : null, Summarize(flag));
        }, ct);

    /// <summary>Flips a flag's master on/off switch (kill switch). Returns the new ruleset version.</summary>
    public Task<long> SetEnabledAsync(string flagKey, bool enabled, string actor, CancellationToken ct = default)
        => UpdateFlagAsync(flagKey, "toggle", actor,
            flag => $"Enabled={flag.Enabled}", f => f.Enabled = enabled, ct);

    /// <summary>Sets the fixed fallthrough variation (clears any fallthrough rollout). Returns the new version.</summary>
    public Task<long> SetFallthroughVariationAsync(string flagKey, int variationIndex, string actor, CancellationToken ct = default)
        => UpdateFlagAsync(flagKey, "set-fallthrough", actor,
            flag => $"fallthrough={DescribeFallthrough(flag)}",
            f => { f.FallthroughVariationIndex = variationIndex; f.FallthroughRollout = null; }, ct);

    /// <summary>Sets an arbitrary fallthrough rollout. Returns the new version.</summary>
    public Task<long> SetFallthroughRolloutAsync(string flagKey, Rollout rollout, string actor, CancellationToken ct = default)
        => UpdateFlagAsync(flagKey, "set-rollout", actor,
            flag => $"fallthrough={DescribeFallthrough(flag)}",
            f => f.FallthroughRollout = rollout, ct);

    /// <summary>
    /// Convenience for the common case: roll a boolean flag out to <paramref name="percent"/>% of users
    /// as a sticky fallthrough rollout (variation 0 = true, 1 = false). Returns the new version.
    /// </summary>
    public Task<long> SetBooleanRolloutAsync(string flagKey, double percent, string actor, CancellationToken ct = default)
        => UpdateFlagAsync(flagKey, "set-rollout", actor,
            flag => $"fallthrough={DescribeFallthrough(flag)}",
            f => f.FallthroughRollout = Rollout.Percentage(0, 1, percent), ct);

    /// <summary>
    /// Generic CAS edit of one existing flag. <paramref name="mutate"/> receives a private clone to
    /// edit in place; <paramref name="describeBefore"/> captures the old-value summary for the audit.
    /// </summary>
    public Task<long> UpdateFlagAsync(
        string flagKey, string action, string actor,
        Func<FlagDefinition, string> describeBefore,
        Action<FlagDefinition> mutate,
        CancellationToken ct = default)
        => ApplyAsync(actor, rs =>
        {
            if (!rs.Flags.TryGetValue(flagKey, out var flag))
                throw new FlagStoreException($"Flag '{flagKey}' does not exist.");
            var old = describeBefore(flag);
            mutate(flag);
            return new AuditDraft(flagKey, action, old, describeBefore(flag));
        }, ct);

    // ---- the one CAS engine every mutation funnels through ----

    private async Task<long> ApplyAsync(string actor, Func<RulesetDocument, AuditDraft?> mutate, CancellationToken ct)
    {
        const int maxAttempts = 256;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var current = await _store.ReadAsync<RulesetDocument>(Keys.Ruleset, ct).ConfigureAwait(false);
            var working = current.Found && current.Value is not null ? current.Value.Clone() : new RulesetDocument();

            var draft = mutate(working);
            if (draft is null)
                return working.Version; // idempotent no-op.

            working.Version += 1;
            working.UpdatedAtUtc = _now();
            working.UpdatedBy = actor;

            var committed = current.Found
                ? await _store.CompareAndSwapAsync(Keys.Ruleset, current.Version, working, ct: ct).ConfigureAwait(false)
                : await _store.CreateAsync(Keys.Ruleset, working, ct: ct).ConfigureAwait(false);

            if (!committed)
                continue; // another admin won the race — re-read their state and re-apply.

            await AppendAuditAsync(actor, draft.Value, working.Version, ct).ConfigureAwait(false);
            return working.Version;
        }
        throw new FlagStoreException($"Ruleset CAS retry budget exhausted after {maxAttempts} attempts (admin contention).");
    }

    private Task AppendAuditAsync(string actor, AuditDraft draft, long rulesetVersion, CancellationToken ct)
        => _store.MutateAsync<AuditLog>(
            Keys.Audit,
            log =>
            {
                log.Entries.Add(new AuditEntry
                {
                    Seq = log.NextSeq,
                    AtUtc = _now(),
                    Actor = actor,
                    FlagKey = draft.FlagKey,
                    Action = draft.Action,
                    Old = draft.Old,
                    New = draft.New,
                    RulesetVersion = rulesetVersion
                });
                log.NextSeq += 1;
                return log;
            },
            create: () => new AuditLog(),
            ct: ct);

    // ---- audit summaries ----

    private static string Summarize(FlagDefinition f)
        => $"{f.Kind} enabled={f.Enabled} off={f.OffVariationIndex} variations=[{string.Join(",", f.Variations)}] " +
           $"targets={f.Targets.Count} rules={f.Rules.Count} fallthrough={DescribeFallthrough(f)}";

    private static string DescribeFallthrough(FlagDefinition f)
        => f.FallthroughRollout is { } r
            ? "rollout[" + string.Join(",", r.Buckets.Select(b => $"v{b.VariationIndex}:{b.Weight}")) + "]"
            : $"v{f.FallthroughVariationIndex}";
}
