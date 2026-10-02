using System.Security.Cryptography;
using System.Text;
using FeatureFlags.Model;

namespace FeatureFlags.Evaluation;

/// <summary>
/// Deterministic, sticky bucketing — the heart of consistent percentage rollouts.
///
/// A user's bucket value is derived purely from (flag key · salt · bucketing id) via SHA-1, mapped
/// into [0,1). It depends on <b>no server state and no clock</b>, so every client computes the same
/// value for the same user, and a user keeps the same value across evaluations forever (sticky).
/// The function is well-distributed, so hashing a large population yields a near-uniform spread —
/// that is what makes "20%" admit ~20% of users.
///
/// This is the LaunchDarkly bucketing scheme: SHA-1 of "<c>key.salt.id</c>", take the first 15 hex
/// digits (60 bits) as an integer, divide by 2^60.
/// </summary>
public static class Bucketing
{
    private const long ScaleDenominator = 1L << 60; // 2^60; 15 hex digits = 60 bits

    /// <summary>The user's bucket value in [0,1) for a given flag + rollout.</summary>
    public static double BucketValue(string flagKey, Rollout rollout, UserContext user)
    {
        var salt = string.IsNullOrEmpty(rollout.Salt) ? flagKey : rollout.Salt;
        var bucketBy = string.IsNullOrEmpty(rollout.BucketBy) ? "key" : rollout.BucketBy;
        var id = user.TryGetAttribute(bucketBy, out var v) ? v : user.Key;
        return Hash($"{flagKey}.{salt}.{id}");
    }

    /// <summary>Raw hash of an arbitrary string into [0,1). Exposed for distribution testing.</summary>
    public static double Hash(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        // First 15 hex digits = first 7.5 bytes. Build a 60-bit integer from the top 60 bits.
        long acc = 0;
        for (var i = 0; i < 7; i++)                 // 7 bytes = 56 bits
            acc = (acc << 8) | bytes[i];
        acc = (acc << 4) | (uint)(bytes[7] >> 4);   // + top 4 bits of the 8th byte = 60 bits
        return (double)acc / ScaleDenominator;
    }

    /// <summary>
    /// Selects the variation a rollout assigns to this user. Walks the ordered buckets, accumulating
    /// weight, and returns the first bucket whose cumulative share the bucket value falls under.
    /// Because the buckets are ordered and cumulative, widening the first bucket only ever absorbs
    /// more users into it — nobody already in it drops out (monotonic ⇒ sticky across % changes).
    /// </summary>
    public static int SelectVariation(string flagKey, Rollout rollout, UserContext user)
    {
        if (rollout.Buckets.Count == 0) return 0;
        var bucket = BucketValue(flagKey, rollout, user);
        double cumulative = 0;
        foreach (var b in rollout.Buckets)
        {
            cumulative += b.Weight / 100000d;
            if (bucket < cumulative) return b.VariationIndex;
        }
        // Rounding slack: fall back to the last bucket.
        return rollout.Buckets[^1].VariationIndex;
    }
}
