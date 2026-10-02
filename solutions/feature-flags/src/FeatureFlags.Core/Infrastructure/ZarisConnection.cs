using Clustron.Zaris.Client;
using Clustron.Zaris.InProc;

namespace FeatureFlags.Infrastructure;

/// <summary>
/// Opens a connection to the Zaris store that backs the feature-flag service — the versioned ruleset
/// document and the audit log both live in one store.
///
/// Two deployment shapes, selected only by the connection string — the code is identical for both:
/// <list type="bullet">
///   <item><c>zaris://inproc/&lt;store&gt;</c> — an embedded, in-process engine (no external cluster).
///         Every <see cref="IZarisClient"/> connection <b>in the same process</b> to the same store
///         name shares one backing engine, so the admin (one client) and many SDK clients (each with
///         its own connection) all talk to one logical store. This is what the demo and the
///         integration tests use, which is why the whole thing is self-contained.</item>
///   <item><c>zaris://host:port/&lt;store&gt;</c> — a networked Zaris node/cluster, for sharing the
///         same ruleset across separate OS processes / machines. The service code does not change.</item>
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
