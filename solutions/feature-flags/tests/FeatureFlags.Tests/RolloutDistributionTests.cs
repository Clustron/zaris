using FeatureFlags.Evaluation;
using FeatureFlags.Model;
using Xunit;
using Xunit.Abstractions;

namespace FeatureFlags.Tests;

/// <summary>
/// Properties of the deterministic, sticky percentage rollout: accurate population split within
/// tolerance, per-user stickiness across repeated evaluations, and monotonic (subset) growth when the
/// percentage is increased — the properties that make a rollout safe to crank up without churn.
/// </summary>
public sealed class RolloutDistributionTests
{
    private readonly ITestOutputHelper _out;
    public RolloutDistributionTests(ITestOutputHelper output) => _out = output;

    private const int Population = 20_000;

    private static FlagDefinition BoolWithRollout(double percent)
    {
        var flag = FlagDefinition.Boolean("rollout-flag", enabled: true, fallthrough: false);
        flag.FallthroughRollout = Rollout.Percentage(0, 1, percent); // v0=true=on, v1=false=off
        return flag;
    }

    private static RulesetSnapshot Snap(FlagDefinition f) => new(1, new Dictionary<string, FlagDefinition> { [f.Key] = f });

    private static HashSet<string> AdmittedAt(double percent)
    {
        var snap = Snap(BoolWithRollout(percent));
        var admitted = new HashSet<string>();
        for (var i = 0; i < Population; i++)
        {
            var key = $"user-{i}";
            if (FlagEvaluator.Evaluate(snap, "rollout-flag", new UserContext(key)).Value == "true")
                admitted.Add(key);
        }
        return admitted;
    }

    [Theory]
    [InlineData(20.0)]
    [InlineData(50.0)]
    [InlineData(5.0)]
    public void Rollout_admits_the_target_fraction_within_tolerance(double percent)
    {
        var admitted = AdmittedAt(percent).Count;
        var actual = 100.0 * admitted / Population;
        _out.WriteLine($"target={percent:0.0}%  admitted={admitted}/{Population} = {actual:0.00}%");
        Assert.InRange(actual, percent - 1.5, percent + 1.5); // ±1.5 points (well beyond statistical noise)
    }

    [Fact]
    public void Same_user_gets_the_same_result_every_time_sticky()
    {
        var snap = Snap(BoolWithRollout(37));
        for (var i = 0; i < 2_000; i++)
        {
            var u = new UserContext($"user-{i}");
            var first = FlagEvaluator.Evaluate(snap, "rollout-flag", u).Value;
            for (var r = 0; r < 5; r++)
                Assert.Equal(first, FlagEvaluator.Evaluate(snap, "rollout-flag", u).Value);
        }
    }

    [Fact]
    public void Increasing_the_percentage_only_adds_users_never_removes_them()
    {
        // Monotonic growth: everyone in at 10% is still in at 20% is still in at 60%.
        var at10 = AdmittedAt(10);
        var at20 = AdmittedAt(20);
        var at60 = AdmittedAt(60);

        Assert.ProperSubset(at20, at10);  // at10 is a proper subset of at20
        Assert.ProperSubset(at60, at20);  // at20 is a proper subset of at60
        _out.WriteLine($"10%→{at10.Count}  20%→{at20.Count}  60%→{at60.Count} (each strictly contains the previous)");
    }

    [Fact]
    public void Different_flags_bucket_independently()
    {
        // Two flags at 20% should not admit the exact same user set (salt = flag key differs).
        var a = Snap(BoolWithRollout(20));          // salt defaults to flag key "rollout-flag"
        var flagB = FlagDefinition.Boolean("other-flag", enabled: true, fallthrough: false);
        flagB.FallthroughRollout = Rollout.Percentage(0, 1, 20);
        var b = new RulesetSnapshot(1, new Dictionary<string, FlagDefinition> { [flagB.Key] = flagB });

        var overlap = 0;
        for (var i = 0; i < Population; i++)
        {
            var u = new UserContext($"user-{i}");
            var inA = FlagEvaluator.Evaluate(a, "rollout-flag", u).Value == "true";
            var inB = FlagEvaluator.Evaluate(b, "other-flag", u).Value == "true";
            if (inA && inB) overlap++;
        }
        // Independent 20% sets overlap ~4% of the population (0.2*0.2); assert it's not identical.
        var overlapFraction = 100.0 * overlap / Population;
        _out.WriteLine($"overlap of two independent 20% rollouts = {overlapFraction:0.0}% of population");
        Assert.InRange(overlapFraction, 2.0, 6.0);
    }

    [Fact]
    public void Multivariate_weighted_rollout_splits_three_ways()
    {
        var flag = FlagDefinition.Multivariate("exp", "control", "variantA", "variantB");
        flag.FallthroughRollout = new Rollout
        {
            Buckets =
            {
                new RolloutBucket(0, 50_000), // 50% control
                new RolloutBucket(1, 30_000), // 30% A
                new RolloutBucket(2, 20_000)  // 20% B
            }
        };
        var snap = Snap(flag);
        var counts = new Dictionary<string, int> { ["control"] = 0, ["variantA"] = 0, ["variantB"] = 0 };
        for (var i = 0; i < Population; i++)
            counts[FlagEvaluator.Evaluate(snap, "exp", new UserContext($"user-{i}")).Value!]++;

        _out.WriteLine($"control={counts["control"]} A={counts["variantA"]} B={counts["variantB"]}");
        Assert.InRange(100.0 * counts["control"] / Population, 48.5, 51.5);
        Assert.InRange(100.0 * counts["variantA"] / Population, 28.5, 31.5);
        Assert.InRange(100.0 * counts["variantB"] / Population, 18.5, 21.5);
    }
}
