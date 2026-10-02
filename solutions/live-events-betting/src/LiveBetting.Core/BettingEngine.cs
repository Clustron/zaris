using Clustron.Zaris.Client;
using LiveBetting.Core.Infrastructure;
using LiveBetting.Core.Services;

namespace LiveBetting.Core;

/// <summary>
/// Composition root for the live-events betting engine: connects to one Zaris store (embedded by
/// default) and wires the wallet, market, betting and settlement services over the single shared
/// <see cref="IZaris"/> client. Dispose to release the connection.
///
/// Because every in-process connection to the same inproc store name shares one backing engine, a
/// driver, a feed publisher and many bettor threads can each hold their own <see cref="BettingEngine"/>
/// against <c>zaris://inproc/&lt;store&gt;</c> and still operate on one logical store.
/// </summary>
public sealed class BettingEngine : IAsyncDisposable
{
    public IZaris Zaris { get; }
    public DocumentStore Store { get; }
    public WalletService Wallets { get; }
    public MarketService Markets { get; }
    public BettingService Betting { get; }
    public SettlementService Settlement { get; }

    private BettingEngine(IZaris zaris, IClock clock)
    {
        Zaris = zaris;
        Store = new DocumentStore(zaris);
        Wallets = new WalletService(Store);
        Markets = new MarketService(Store, zaris, clock);
        Betting = new BettingService(Store, Wallets, Markets, zaris, clock);
        Settlement = new SettlementService(Store, Wallets, Markets, zaris, clock);
    }

    /// <summary>
    /// Connects and builds the engine. With no connection string an embedded (in-process, no cluster)
    /// store is used; pass e.g. <c>zaris://127.0.0.1:7861/betting</c> to target a real local node.
    /// </summary>
    public static async Task<BettingEngine> ConnectAsync(
        string connectionString = "zaris://inproc/betting",
        IClock? clock = null,
        CancellationToken ct = default)
    {
        var zaris = await ZarisConnection.ConnectAsync(connectionString, ct);
        return new BettingEngine(zaris, clock ?? SystemClock.Instance);
    }

    public ValueTask DisposeAsync()
    {
        Zaris.Dispose();
        return ValueTask.CompletedTask;
    }
}
