using LeaderboardPresence.Api.Hubs;
using LeaderboardPresence.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeaderboardPresence.Api;

/// <summary>Builds the ASP.NET Core app: embedded Zaris system, REST endpoints, SignalR hub, Zaris→SignalR bridge.</summary>
public static class AppBuilder
{
    public sealed record ScoreRequest(string Player, double Delta);
    public sealed record HeartbeatRequest(string? Detail);
    public sealed record StatusRequest(PresenceStatus Status, string? Detail);

    public static async Task<WebApplication> BuildAsync(
        string url,
        string connectionString = "zaris://inproc/leaderboard",
        PresenceOptions? presenceOptions = null,
        TimeSpan? sweepInterval = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls(url);

        // One embedded Zaris-backed system shared by the whole app.
        var system = await LeaderboardPresenceSystem.ConnectAsync(
            connectionString,
            new LeaderboardOptions(),
            presenceOptions ?? new PresenceOptions(),
            SystemClock.Instance);

        builder.Services.AddSingleton(system);
        builder.Services.AddSingleton<ILeaderboardService>(system.Leaderboard);
        builder.Services.AddSingleton<IPresenceService>(system.Presence);
        builder.Services.AddSignalR();
        builder.Services.AddHostedService<ZarisLiveBridge>();
        builder.Services.AddSingleton<IHostedService>(sp => new PresenceSweeperService(
            sp.GetRequiredService<LeaderboardPresenceSystem>(),
            sp.GetRequiredService<ILogger<PresenceSweeperService>>(),
            sweepInterval ?? TimeSpan.FromSeconds(5)));

        var app = builder.Build();

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        // ── leaderboard ──
        app.MapPost("/api/boards/{board}/scores", async (string board, ScoreRequest req, ILeaderboardService lb) =>
            Results.Ok(await lb.SubmitScoreAsync(board, req.Player, req.Delta)));

        app.MapGet("/api/boards/{board}/top", async (string board, ILeaderboardService lb, int count = 10, LeaderboardWindow window = LeaderboardWindow.AllTime) =>
            Results.Ok(await lb.GetTopAsync(board, count, window)));

        app.MapGet("/api/boards/{board}/players/{player}", async (string board, string player, ILeaderboardService lb, LeaderboardWindow window = LeaderboardWindow.AllTime) =>
        {
            var r = await lb.GetRankAsync(board, player, window);
            return r is null ? Results.NotFound() : Results.Ok(r);
        });

        app.MapGet("/api/boards/{board}/players/{player}/neighbors", async (string board, string player, ILeaderboardService lb, int radius = 3, LeaderboardWindow window = LeaderboardWindow.AllTime) =>
            Results.Ok(await lb.GetNeighborsAsync(board, player, radius, window)));

        app.MapGet("/api/boards/{board}/count", async (string board, ILeaderboardService lb, LeaderboardWindow window = LeaderboardWindow.AllTime) =>
            Results.Ok(new { board, window = window.ToString(), count = await lb.GetCountAsync(board, window) }));

        app.MapGet("/api/boards/{board}/audit", async (string board, ILeaderboardService lb, int count = 20) =>
            Results.Ok(await lb.ReadAuditAsync(board, count)));

        // ── presence ──
        app.MapPost("/api/presence/{player}/heartbeat", async (string player, HeartbeatRequest? req, IPresenceService p) =>
            Results.Ok(await p.HeartbeatAsync(player, req?.Detail)));

        app.MapPost("/api/presence/{player}/status", async (string player, StatusRequest req, IPresenceService p) =>
            Results.Ok(await p.SetStatusAsync(player, req.Status, req.Detail)));

        app.MapGet("/api/presence/online", async (IPresenceService p) => Results.Ok(await p.ListOnlineAsync()));
        app.MapGet("/api/presence/count", async (IPresenceService p) => Results.Ok(new { online = await p.OnlineCountAsync() }));
        app.MapGet("/api/presence/{player}", async (string player, IPresenceService p) => Results.Ok(await p.GetAsync(player)));

        app.MapHub<LiveHub>("/hub/live");

        return app;
    }
}
