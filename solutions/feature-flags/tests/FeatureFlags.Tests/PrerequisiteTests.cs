using FeatureFlags.Evaluation;
using FeatureFlags.Model;
using Xunit;

namespace FeatureFlags.Tests;

/// <summary>Prerequisite-flag chaining and default fallback behaviour.</summary>
public sealed class PrerequisiteTests
{
    private static RulesetSnapshot Snapshot(params FlagDefinition[] flags)
        => new(1, flags.ToDictionary(f => f.Key));

    private static FlagDefinition Dependent()
    {
        var f = FlagDefinition.Boolean("feature", enabled: true, fallthrough: true); // would be true on its own
        f.Prerequisites.Add(new Prerequisite("masterGate", 0));                       // requires masterGate = true
        return f;
    }

    [Fact]
    public void Satisfied_prerequisite_allows_normal_evaluation()
    {
        var gate = FlagDefinition.Boolean("masterGate", enabled: true, fallthrough: true); // serves true (index 0)
        var d = FlagEvaluator.Evaluate(Snapshot(gate, Dependent()), "feature", new UserContext("u"));
        Assert.Equal(EvalReason.Fallthrough, d.Reason);
        Assert.Equal("true", d.Value);
    }

    [Fact]
    public void Prerequisite_serving_wrong_variation_forces_off()
    {
        var gate = FlagDefinition.Boolean("masterGate", enabled: true, fallthrough: false); // serves false (index 1)
        var d = FlagEvaluator.Evaluate(Snapshot(gate, Dependent()), "feature", new UserContext("u"));
        Assert.Equal(EvalReason.PrerequisiteFailed, d.Reason);
        Assert.Equal("false", d.Value); // dependent's off variation
    }

    [Fact]
    public void Disabled_prerequisite_forces_off()
    {
        var gate = FlagDefinition.Boolean("masterGate", enabled: false, fallthrough: true);
        var d = FlagEvaluator.Evaluate(Snapshot(gate, Dependent()), "feature", new UserContext("u"));
        Assert.Equal(EvalReason.PrerequisiteFailed, d.Reason);
        Assert.Equal("false", d.Value);
    }

    [Fact]
    public void Missing_prerequisite_forces_off()
    {
        // masterGate not in the ruleset at all.
        var d = FlagEvaluator.Evaluate(Snapshot(Dependent()), "feature", new UserContext("u"));
        Assert.Equal(EvalReason.PrerequisiteFailed, d.Reason);
        Assert.Equal("false", d.Value);
    }

    [Fact]
    public void Prerequisite_cycle_is_broken_and_defaults_off()
    {
        var a = FlagDefinition.Boolean("a", enabled: true, fallthrough: true);
        a.Prerequisites.Add(new Prerequisite("b", 0));
        var b = FlagDefinition.Boolean("b", enabled: true, fallthrough: true);
        b.Prerequisites.Add(new Prerequisite("a", 0));
        var d = FlagEvaluator.Evaluate(Snapshot(a, b), "a", new UserContext("u"));
        Assert.Equal(EvalReason.PrerequisiteFailed, d.Reason); // cycle guard trips, no stack overflow
    }
}
