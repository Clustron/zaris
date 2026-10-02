using Clustron.Zaris.Client;
using Clustron.Zaris.InProc;

namespace JobQueue.Infrastructure;

/// <summary>
/// Opens a connection to the Zaris store that backs the whole queue — the job documents, the active
/// index, the dead-letter index and the TTL lease markers all live in one store.
///
/// Two deployment shapes, selected only by the connection string — the code is identical for both:
/// <list type="bullet">
///   <item><c>zaris://inproc/&lt;store&gt;</c> — an embedded, in-process engine (no external cluster).
///         Every <see cref="IZarisClient"/> connection <b>in the same process</b> to the same store
///         name shares one backing engine, so many producers and many competing workers (each with
///         its own client) all talk to one logical store. This is what the demo and the integration
///         tests use, which is why the whole thing is self-contained.</item>
///   <item><c>zaris://host:port/&lt;store&gt;</c> — a networked Zaris node/cluster, for sharing the
///         same queue across separate OS processes / machines. The queue code does not change.</item>
/// </list>
/// </summary>
public static class ZarisConnection
{
    private static int _bootstrapped;

    /// <summary>Connects to <paramref name="connectionString"/>, registering the in-proc engine on first use.</summary>
    public static async Task<IZarisClient> ConnectAsync(string connectionString, CancellationToken ct = default)
    {
        // Register the in-proc client factory exactly once, before the first inproc ConnectAsync.
        if (connectionString.Contains("inproc", StringComparison.OrdinalIgnoreCase)
            && Interlocked.Exchange(ref _bootstrapped, 1) == 0)
        {
            new InProcBootstrap().Register();
        }

        return await ZarisClient.ConnectAsync(connectionString, ct).ConfigureAwait(false);
    }
}
