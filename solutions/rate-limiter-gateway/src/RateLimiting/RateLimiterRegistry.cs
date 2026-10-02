using System.Collections.Generic;

namespace RateLimiterGateway.RateLimiting;

/// <summary>Resolves the limiter implementation for a policy's algorithm.</summary>
public interface IRateLimiterRegistry
{
    IZarisRateLimiter For(RateLimitAlgorithm algorithm);
}

public sealed class RateLimiterRegistry : IRateLimiterRegistry
{
    private readonly Dictionary<RateLimitAlgorithm, IZarisRateLimiter> _byAlgo = new();

    public RateLimiterRegistry(IEnumerable<IZarisRateLimiter> limiters)
    {
        foreach (var l in limiters) _byAlgo[l.Algorithm] = l;
    }

    public IZarisRateLimiter For(RateLimitAlgorithm algorithm)
        => _byAlgo.TryGetValue(algorithm, out var l)
            ? l
            : throw new ZarisRateLimiterException($"No limiter registered for algorithm {algorithm}.");
}
