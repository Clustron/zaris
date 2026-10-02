using System;

namespace RateLimiterGateway.RateLimiting;

/// <summary>Abstracts "now" so the window/refill math is deterministically testable
/// (the unit tests advance a <see cref="TestClock"/> to exercise expiry and refill).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Wall-clock implementation used in production.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>A manually-advanced clock for tests.</summary>
public sealed class TestClock : IClock
{
    private long _ms;
    public TestClock(DateTimeOffset? start = null)
        => _ms = (start ?? DateTimeOffset.UnixEpoch).ToUnixTimeMilliseconds();

    public DateTimeOffset UtcNow => DateTimeOffset.FromUnixTimeMilliseconds(System.Threading.Interlocked.Read(ref _ms));

    public void Advance(TimeSpan by) => System.Threading.Interlocked.Add(ref _ms, (long)by.TotalMilliseconds);
}
