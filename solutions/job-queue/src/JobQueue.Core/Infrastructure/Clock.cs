namespace JobQueue.Infrastructure;

/// <summary>
/// The single source of "now" for lease math. It is injectable for one reason: the correctness of the
/// queue (lease expiry → requeue, retry backoff, scheduled visibility) is decided by comparing a
/// stored UTC timestamp against <see cref="UtcNow"/>. A test can therefore drive those decisions
/// deterministically with <see cref="ManualClock"/> instead of sleeping.
///
/// Note the deliberate separation of concerns: this logical clock governs the <b>authoritative</b>
/// lease (the <c>LeaseExpiresAt</c> field on the durable job document, which must never disappear).
/// The <i>separate</i> write-time Zaris TTL on the in-flight lease marker key uses the store's own
/// wall clock and is auxiliary (self-cleaning in-flight set + monitoring) — see <c>LeaseMarker</c>.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Wall-clock implementation used by the demo and in production.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>A hand-cranked clock for deterministic tests of lease/backoff/schedule timing.</summary>
public sealed class ManualClock : IClock
{
    private long _ticks;

    public ManualClock(DateTimeOffset start) => _ticks = start.UtcTicks;

    public DateTimeOffset UtcNow => new(_ticks, TimeSpan.Zero);

    /// <summary>Advances the logical clock by <paramref name="delta"/>.</summary>
    public void Advance(TimeSpan delta) => Interlocked.Add(ref _ticks, delta.Ticks);
}
