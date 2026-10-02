using System.Threading.Channels;
using LeaderboardPresence.Api.Hubs;
using LeaderboardPresence.Core;
using Microsoft.AspNetCore.SignalR;

namespace LeaderboardPresence.Api;

/// <summary>
/// Bridges Zaris native change notifications to connected SignalR clients. It subscribes once (cluster-wide pattern
/// subscribe for all boards, plus presence) to the Zaris pub/sub fabric and relays every event into the matching hub
/// group. This is the seam that turns Zaris's server-side fan-out into browser/WebSocket push.
///
/// The Zaris pub/sub callback only enqueues onto an in-memory channel; a background pump performs the actual
/// (potentially slow) WebSocket sends. That deliberately decouples the write path from fan-out I/O, so a burst of
/// score submits is never throttled by how fast connected browsers can be written to.
/// </summary>
public sealed class ZarisLiveBridge : IHostedService
{
    private readonly LeaderboardPresenceSystem _sys;
    private readonly IHubContext<LiveHub> _hub;
    private readonly ILogger<ZarisLiveBridge> _log;
    private readonly Channel<(string group, string method, object payload)> _queue =
        Channel.CreateUnbounded<(string, string, object)>(new UnboundedChannelOptions { SingleReader = true });
    private IAsyncDisposable? _lbSub;
    private IAsyncDisposable? _presenceSub;
    private Task? _pump;
    private readonly CancellationTokenSource _cts = new();

    public ZarisLiveBridge(LeaderboardPresenceSystem sys, IHubContext<LiveHub> hub, ILogger<ZarisLiveBridge> log)
    {
        _sys = sys;
        _hub = hub;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _pump = Task.Run(PumpAsync);

        _lbSub = await _sys.Leaderboard.SubscribeChangesAsync(change =>
        {
            _queue.Writer.TryWrite((LiveHub.BoardGroup(change.Board), "leaderboardChanged", change));
            return Task.CompletedTask;
        }, board: null, ct);

        _presenceSub = await _sys.Presence.SubscribeAsync(change =>
        {
            _queue.Writer.TryWrite((LiveHub.PresenceGroup, "presenceChanged", change));
            return Task.CompletedTask;
        }, ct);

        _log.LogInformation("ZarisLiveBridge subscribed to leaderboard + presence change streams.");
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var (group, method, payload) in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                try { await _hub.Clients.Group(group).SendAsync(method, payload, _cts.Token); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _log.LogDebug(ex, "live push failed"); }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_lbSub is not null) await _lbSub.DisposeAsync();
        if (_presenceSub is not null) await _presenceSub.DisposeAsync();
        _queue.Writer.TryComplete();
        _cts.Cancel();
        if (_pump is not null)
            try { await _pump; } catch { /* ignore */ }
    }
}

/// <summary>Periodically sweeps expired presence entries so offline transitions fire even without reads.</summary>
public sealed class PresenceSweeperService : BackgroundService
{
    private readonly LeaderboardPresenceSystem _sys;
    private readonly ILogger<PresenceSweeperService> _log;
    private readonly TimeSpan _interval;

    public PresenceSweeperService(LeaderboardPresenceSystem sys, ILogger<PresenceSweeperService> log, TimeSpan? interval = null)
    {
        _sys = sys;
        _log = log;
        _interval = interval ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var reaped = await _sys.Presence.SweepAsync(stoppingToken);
                if (reaped.Count > 0) _log.LogInformation("Presence sweep reaped {Count} offline players.", reaped.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogWarning(ex, "presence sweep failed"); }

            try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
