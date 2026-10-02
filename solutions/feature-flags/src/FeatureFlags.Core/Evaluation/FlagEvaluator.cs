using System.Globalization;
using System.Text.RegularExpressions;
using FeatureFlags.Model;

namespace FeatureFlags.Evaluation;

/// <summary>
/// The pure, side-effect-free evaluation engine. Given a <see cref="RulesetSnapshot"/>, a flag key
/// and a <see cref="UserContext"/>, it returns an <see cref="EvalDetail"/> with no I/O — identical
/// inputs always give identical outputs. Every client runs this locally against its cached snapshot,
/// so a flag check never touches the network.
///
/// Evaluation order (LaunchDarkly-style): off switch → prerequisites → explicit targets →
/// targeting rules (first match) → fallthrough (fixed or rollout).
/// </summary>
public static class FlagEvaluator
{
    public static EvalDetail Evaluate(RulesetSnapshot snapshot, string flagKey, UserContext user)
        => Evaluate(snapshot, flagKey, user, new HashSet<string>());

    private static EvalDetail Evaluate(RulesetSnapshot snapshot, string flagKey, UserContext user, HashSet<string> visiting)
    {
        if (!snapshot.Flags.TryGetValue(flagKey, out var flag))
            return NotFound(flagKey, snapshot.Version);

        // 1. Master switch.
        if (!flag.Enabled)
            return Resolve(flag, flag.OffVariationIndex, EvalReason.Off, snapshot.Version);

        // 2. Prerequisites — each must be on AND serving its required variation (cycle-guarded).
        foreach (var prereq in flag.Prerequisites)
        {
            if (!visiting.Add(flagKey + "->" + prereq.FlagKey))
                return Resolve(flag, flag.OffVariationIndex, EvalReason.PrerequisiteFailed, snapshot.Version);

            var prereqResult = Evaluate(snapshot, prereq.FlagKey, user, visiting);
            var prereqOn = snapshot.Flags.TryGetValue(prereq.FlagKey, out var pf) && pf.Enabled;
            if (!prereqOn || prereqResult.VariationIndex != prereq.RequiredVariationIndex)
                return Resolve(flag, flag.OffVariationIndex, EvalReason.PrerequisiteFailed, snapshot.Version);
        }

        // 3. Explicit per-user targets.
        foreach (var target in flag.Targets)
            if (target.UserKeys.Contains(user.Key))
                return Resolve(flag, target.VariationIndex, EvalReason.TargetMatch, snapshot.Version);

        // 4. Targeting rules — first fully-matching rule wins.
        foreach (var rule in flag.Rules)
        {
            if (rule.Clauses.All(c => ClauseMatches(c, user)))
            {
                var index = rule.Rollout is { } ro
                    ? Bucketing.SelectVariation(flag.Key, ro, user)
                    : rule.VariationIndex;
                return Resolve(flag, index, EvalReason.RuleMatch, snapshot.Version);
            }
        }

        // 5. Fallthrough — a rollout if configured, else the fixed fallthrough variation.
        var fallthroughIndex = flag.FallthroughRollout is { } fro
            ? Bucketing.SelectVariation(flag.Key, fro, user)
            : flag.FallthroughVariationIndex;
        return Resolve(flag, fallthroughIndex, EvalReason.Fallthrough, snapshot.Version);
    }

    private static bool ClauseMatches(Clause clause, UserContext user)
    {
        if (!user.TryGetAttribute(clause.Attribute, out var attr))
            return clause.Op == ClauseOp.NotIn; // absent attribute: only NotIn can be satisfied.

        switch (clause.Op)
        {
            case ClauseOp.In:
                return clause.Values.Contains(attr);
            case ClauseOp.NotIn:
                return !clause.Values.Contains(attr);
            case ClauseOp.Contains:
                return clause.Values.Any(v => attr.Contains(v, StringComparison.Ordinal));
            case ClauseOp.StartsWith:
                return clause.Values.Any(v => attr.StartsWith(v, StringComparison.Ordinal));
            case ClauseOp.GreaterThan:
                return TryNum(attr, out var a1) && clause.Values.Count > 0 && TryNum(clause.Values[0], out var b1) && a1 > b1;
            case ClauseOp.LessThan:
                return TryNum(attr, out var a2) && clause.Values.Count > 0 && TryNum(clause.Values[0], out var b2) && a2 < b2;
            case ClauseOp.Matches:
                return clause.Values.Count > 0 && SafeRegex(clause.Values[0], attr);
            default:
                return false;
        }
    }

    private static bool TryNum(string s, out double value)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool SafeRegex(string pattern, string input)
    {
        try { return Regex.IsMatch(input, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
        catch { return false; }
    }

    private static EvalDetail Resolve(FlagDefinition flag, int index, EvalReason reason, long version)
    {
        if (index < 0 || index >= flag.Variations.Count)
            return new EvalDetail { FlagKey = flag.Key, Value = null, VariationIndex = -1, Reason = EvalReason.Error, RulesetVersion = version };
        return new EvalDetail
        {
            FlagKey = flag.Key,
            Value = flag.Variations[index],
            VariationIndex = index,
            Reason = reason,
            RulesetVersion = version
        };
    }

    private static EvalDetail NotFound(string flagKey, long version)
        => new() { FlagKey = flagKey, Value = null, VariationIndex = -1, Reason = EvalReason.FlagNotFound, RulesetVersion = version };
}
