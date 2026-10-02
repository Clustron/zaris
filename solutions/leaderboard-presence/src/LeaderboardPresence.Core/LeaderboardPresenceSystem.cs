using Clustron.Zaris.Client;
using Clustron.Zaris.InProc;

namespace LeaderboardPresence.Core;

/// <summary>
/// Composition root: connects to a Zaris store (embedded by default) and exposes the leaderboard + presence services
/// over the one shared <see cref="IZaris"/> client. Dispose to release the client.
/// </summary>
public sealed class LeaderboardPresenceSystem : IAsyncDisposable
{
    private static int _bootstrapped;

    public IZaris Zaris { get; }
    public ILeaderboardService Leaderboard { get; }
    public IPresenceService Presence { get; }

    private LeaderboardPresenceSystem(IZaris zaris, LeaderboardOptions lbOpts, PresenceOptions prOpts, IClock clock)
    {
        Zaris = zaris;
        Leaderboard = new LeaderboardService(zaris, clock, lbOpts);
        Presence = new PresenceService(zaris, clock, prOpts);
    }

    /// <summary>
    /// Connect and build the system. With no connection string an embedded (in-process, no cluster) store is used;
    /// pass e.g. <c>zaris://127.0.0.1:7861/leaderboard</c> to target a real local node instead.
    /// </summary>
    public static async Task<LeaderboardPresenceSystem> ConnectAsync(
        string connectionString = "zaris://inproc/leaderboard",
        LeaderboardOptions? leaderboardOptions = null,
        PresenceOptions? presenceOptions = null,
        IClock? clock = null,
        CancellationToken ct = default)
    {
        if (connectionString.Contains("inproc", StringComparison.OrdinalIgnoreCase)
            && Interlocked.Exchange(ref _bootstrapped, 1) == 0)
        {
            new InProcBootstrap().Register();
        }

        var client = (IZaris)await ZarisClient.ConnectAsync(connectionString, ct);
        return new LeaderboardPresenceSystem(client, leaderboardOptions ?? new(), presenceOptions ?? new(), clock ?? SystemClock.Instance);
    }

    public ValueTask DisposeAsync()
    {
        Zaris.Dispose();
        return ValueTask.CompletedTask;
    }
}
