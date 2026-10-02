using System.Threading.Tasks;
using Clustron.Zaris.Client;
using Clustron.Zaris.InProc;

namespace RateLimiterGateway.RateLimiting;

/// <summary>
/// Connects to the Zaris store that backs the distributed limiter.
///
/// Two deployment shapes, selected purely by the connection string:
/// <list type="bullet">
///   <item><c>zaris://inproc/&lt;store&gt;</c> — an embedded, in-process engine. Multiple
///         <see cref="IZarisClient"/> connections <b>in the same process</b> to the same store name
///         share one backing engine, so several gateway instances hosted in one process share state.
///         This is what the self-contained demo and the integration test use.</item>
///   <item><c>zaris://host:port/&lt;store&gt;</c> — a networked Zaris node. Use this to share the
///         limiter state across separate OS processes / machines; the code is identical.</item>
/// </list>
/// </summary>
public static class ZarisStoreFactory
{
    private static int _bootstrapped;

    public static async Task<IZarisClient> ConnectAsync(string connectionString)
    {
        // Ensure the in-proc client factory is registered before the first inproc ConnectAsync.
        if (System.Threading.Interlocked.Exchange(ref _bootstrapped, 1) == 0)
            new InProcBootstrap().Register();

        return await ZarisClient.ConnectAsync(connectionString);
    }
}
