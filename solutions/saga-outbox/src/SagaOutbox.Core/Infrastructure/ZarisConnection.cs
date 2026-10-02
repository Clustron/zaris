using Clustron.Zaris.Client;
using Clustron.Zaris.InProc;

namespace SagaOutbox.Infrastructure;

/// <summary>
/// Opens a connection to the Zaris store that backs every service, the saga log and the event bus.
///
/// Two deployment shapes, selected only by the connection string — the code is identical for both:
/// <list type="bullet">
///   <item><c>zaris://inproc/&lt;store&gt;</c> — an embedded, in-process engine. Every
///         <see cref="IZarisClient"/> connection <b>in the same process</b> to the same store name
///         shares one backing engine, so the four "services", the orchestrator and the relay all
///         talk to one logical store while keeping their own key prefixes. This is what the demo and
///         the integration tests use, which is why the whole thing is self-contained.</item>
///   <item><c>zaris://host:port/&lt;store&gt;</c> — a networked Zaris node/cluster, for sharing the
///         same state across separate OS processes / machines.</item>
/// </list>
/// </summary>
public static class ZarisConnection
{
    private static int _bootstrapped;

    /// <summary>Connects to <paramref name="connectionString"/>, registering the in-proc engine on first use.</summary>
    public static async Task<IZarisClient> ConnectAsync(string connectionString)
    {
        // Register the in-proc client factory exactly once before the first inproc ConnectAsync.
        if (Interlocked.Exchange(ref _bootstrapped, 1) == 0)
            new InProcBootstrap().Register();

        return await ZarisClient.ConnectAsync(connectionString).ConfigureAwait(false);
    }
}
