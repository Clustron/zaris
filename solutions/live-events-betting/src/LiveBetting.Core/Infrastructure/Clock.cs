namespace LiveBetting.Core.Infrastructure;

/// <summary>Time source, injectable so tests are deterministic.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>A hand-advanceable clock for tests.</summary>
public sealed class FakeClock : IClock
{
    private long _ms;
    public FakeClock(DateTimeOffset start) => _ms = start.ToUnixTimeMilliseconds();
    public DateTimeOffset UtcNow => DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Read(ref _ms));
    public void Advance(TimeSpan by) => Interlocked.Add(ref _ms, (long)by.TotalMilliseconds);
}
