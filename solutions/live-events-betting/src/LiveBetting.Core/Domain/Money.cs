using System.Globalization;

namespace LiveBetting.Core.Domain;

/// <summary>
/// Money is carried everywhere as whole <b>minor units</b> (e.g. cents / pence) in a <c>long</c>, never
/// as a floating-point value — a wallet balance that must never go negative and must conserve to the
/// cent cannot be represented in <c>double</c>. Odds, by contrast, are decimal multipliers (e.g. 2.50)
/// and are carried as <see cref="decimal"/>. Payout = round(stake_minor * odds) back to minor units.
/// </summary>
public static class Money
{
    /// <summary>Computes a payout in minor units from a stake in minor units and decimal odds, rounding to the nearest minor unit (half away from zero — the house pays the extra fraction).</summary>
    public static long Payout(long stakeMinor, decimal odds)
    {
        var raw = stakeMinor * odds;
        return (long)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    /// <summary>Formats minor units as a human-readable major amount (two decimal places).</summary>
    public static string Format(long minor) =>
        (minor / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
