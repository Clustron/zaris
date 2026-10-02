using FeatureFlags.Evaluation;
using FeatureFlags.Model;
using Xunit;

namespace FeatureFlags.Tests;

/// <summary>
/// Pure evaluation-engine tests: off switch, explicit targets, clause matching across every operator,
/// rule ordering, multivariate fallthrough, and malformed-flag / not-found defaulting. These need no
/// store — they run the <see cref="FlagEvaluator"/> directly against a hand-built snapshot.
/// </summary>
public sealed class EvaluationTests
{
    private static RulesetSnapshot Snapshot(params FlagDefinition[] flags)
        => new(1, flags.ToDictionary(f => f.Key));

    private static UserContext User(string key) => new(key);

    [Fact]
    public void Disabled_flag_serves_off_variation()
    {
        var flag = FlagDefinition.Boolean("f", enabled: false, fallthrough: true);
        var d = FlagEvaluator.Evaluate(Snapshot(flag), "f", User("u1"));
        Assert.Equal(EvalReason.Off, d.Reason);
        Assert.Equal("false", d.Value);              // off variation = false
        Assert.Equal(1, d.VariationIndex);
    }

    [Fact]
    public void Enabled_flag_with_true_fallthrough_serves_true()
    {
        var flag = FlagDefinition.Boolean("f", enabled: true, fallthrough: true);
        var d = FlagEvaluator.Evaluate(Snapshot(flag), "f", User("u1"));
        Assert.Equal(EvalReason.Fallthrough, d.Reason);
        Assert.Equal("true", d.Value);
    }

    [Fact]
    public void Explicit_target_beats_fallthrough()
    {
        var flag = FlagDefinition.Boolean("f", enabled: true, fallthrough: false);
        flag.Targets.Add(new Target(0, "vip-user"));   // variation 0 = true
        var hit = FlagEvaluator.Evaluate(Snapshot(flag), "f", User("vip-user"));
        var miss = FlagEvaluator.Evaluate(Snapshot(flag), "f", User("other"));
        Assert.Equal(EvalReason.TargetMatch, hit.Reason);
        Assert.Equal("true", hit.Value);
        Assert.Equal("false", miss.Value);
    }

    [Fact]
    public void Missing_flag_defaults_and_flags_the_reason()
    {
        var d = FlagEvaluator.Evaluate(Snapshot(), "nope", User("u1"));
        Assert.Equal(EvalReason.FlagNotFound, d.Reason);
        Assert.True(d.IsDefaulted);
        Assert.Null(d.Value);
    }

    [Theory]
    [InlineData(ClauseOp.In, "country", "US", "US", true)]
    [InlineData(ClauseOp.In, "country", "US", "CA", false)]
    [InlineData(ClauseOp.NotIn, "country", "US", "CA", true)]
    [InlineData(ClauseOp.NotIn, "country", "US", "US", false)]
    [InlineData(ClauseOp.Contains, "email", "@acme.com", "bob@acme.com", true)]
    [InlineData(ClauseOp.Contains, "email", "@acme.com", "bob@other.com", false)]
    [InlineData(ClauseOp.StartsWith, "path", "/admin", "/admin/users", true)]
    [InlineData(ClauseOp.StartsWith, "path", "/admin", "/user", false)]
    [InlineData(ClauseOp.GreaterThan, "age", "18", "21", true)]
    [InlineData(ClauseOp.GreaterThan, "age", "18", "16", false)]
    [InlineData(ClauseOp.LessThan, "age", "18", "16", true)]
    [InlineData(ClauseOp.Matches, "email", @"^admin@.*", "admin@acme.com", true)]
    [InlineData(ClauseOp.Matches, "email", @"^admin@.*", "bob@acme.com", false)]
    public void Clause_operators_match_as_specified(ClauseOp op, string attr, string ruleValue, string userValue, bool expected)
    {
        var flag = FlagDefinition.Boolean("f", enabled: true, fallthrough: false); // fallthrough false
        flag.Rules.Add(new TargetingRule(0, new Clause(attr, op, ruleValue)));      // match => true
        var user = UserContext.For("u1").With(attr, userValue).Build();
        var d = FlagEvaluator.Evaluate(Snapshot(flag), "f", user);
        Assert.Equal(expected, d.Value == "true");
        Assert.Equal(expected ? EvalReason.RuleMatch : EvalReason.Fallthrough, d.Reason);
    }

    [Fact]
    public void All_clauses_in_a_rule_are_anded()
    {
        var flag = FlagDefinition.Boolean("f", enabled: true, fallthrough: false);
        flag.Rules.Add(new TargetingRule(0,
            new Clause("country", ClauseOp.In, "US"),
            new Clause("plan", ClauseOp.In, "pro")));
        var both = UserContext.For("u").With("country", "US").With("plan", "pro").Build();
        var one = UserContext.For("u").With("country", "US").With("plan", "free").Build();
        Assert.True(FlagEvaluator.Evaluate(Snapshot(flag), "f", both).Value == "true");
        Assert.True(FlagEvaluator.Evaluate(Snapshot(flag), "f", one).Value == "false");
    }

    [Fact]
    public void Rules_are_first_match_wins_in_order()
    {
        var flag = FlagDefinition.Multivariate("color", "control", "red", "blue");
        flag.Rules.Add(new TargetingRule(1, new Clause("country", ClauseOp.In, "US"))); // red
        flag.Rules.Add(new TargetingRule(2, new Clause("plan", ClauseOp.In, "pro")));    // blue
        var user = UserContext.For("u").With("country", "US").With("plan", "pro").Build();
        var d = FlagEvaluator.Evaluate(Snapshot(flag), "color", user);
        Assert.Equal("red", d.Value); // first rule wins even though both match
    }

    [Fact]
    public void Multivariate_fallthrough_serves_configured_default()
    {
        var flag = FlagDefinition.Multivariate("color", "control", "red", "blue");
        flag.FallthroughVariationIndex = 2; // blue
        var d = FlagEvaluator.Evaluate(Snapshot(flag), "color", User("u"));
        Assert.Equal("blue", d.Value);
        Assert.Equal(EvalReason.Fallthrough, d.Reason);
    }

    [Fact]
    public void Bad_variation_index_defaults_with_error_reason()
    {
        var flag = FlagDefinition.Multivariate("color", "control", "red");
        flag.FallthroughVariationIndex = 9; // out of range
        var d = FlagEvaluator.Evaluate(Snapshot(flag), "color", User("u"));
        Assert.Equal(EvalReason.Error, d.Reason);
        Assert.Null(d.Value);
    }
}
