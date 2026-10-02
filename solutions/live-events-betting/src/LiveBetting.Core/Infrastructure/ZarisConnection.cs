using Clustron.Zaris.Client;
using Clustron.Zaris.InProc;

namespace LiveBetting.Core.Infrastructure;

/// <summary>
/// Opens the connection to the Zaris store that backs the whole engine — the wallets, markets, bets,
/// open-bet indexes, settlement log, odds pub/sub channels and audit streams all live in one store.
///
/// Two deployment shapes, selected only by the connection string; the code above is identical for both:
/// <list type="bullet">
///   <item><c>zaris://inproc/&lt;store&gt;</c> — an embedded, in-process engine (no external cluster).
///         Every connection to the same store name <b>in the same process</b> shares one backing
///         engine, so independent "services" / bettor threads all talk to one logical store. This is
///         what the demo and tests use, which is why the whole thing is self-contained.</item>
///   <item><c>zaris://host:port/&lt;store&gt;</c> — a networked Zaris node/cluster, for sharing the same
///         state across OS processes / machines. The engine code does not change.</item>
/// </list>
///
/// The result is always cast to <see cref="IZaris"/> so the native data-structure surface (sorted
/// sets for indexes, pub/sub for live odds fan-out, streams for the durable audit/settlement log) is
/// available alongside the plain versioned KV operations.
/// </summary>
public static class ZarisConnection
{
    private static int _bootstrapped;

    public static async Task<IZaris> ConnectAsync(string connectionString, CancellationToken ct = default)
    {
        // Register the in-proc client factory exactly once, before the first inproc ConnectAsync.
        if (connectionString.Contains("inproc", StringComparison.OrdinalIgnoreCase)
            && Interlocked.Exchange(ref _bootstrapped, 1) == 0)
        {
            new InProcBootstrap().Register();
        }

        return (IZaris)await ZarisClient.ConnectAsync(connectionString, ct).ConfigureAwait(false);
    }
}
