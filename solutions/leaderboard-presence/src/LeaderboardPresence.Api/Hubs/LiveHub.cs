using Microsoft.AspNetCore.SignalR;

namespace LeaderboardPresence.Api.Hubs;

/// <summary>
/// The WebSocket/SignalR hub clients connect to for live push. Clients join a board group and/or the presence group;
/// the <see cref="ZarisLiveBridge"/> (fed by Zaris pub/sub) pushes <c>leaderboardChanged</c> / <c>presenceChanged</c>
/// messages into those groups.
/// </summary>
public sealed class LiveHub : Hub
{
    public static string BoardGroup(string board) => $"board:{board}";
    public const string PresenceGroup = "presence";

    public Task JoinBoard(string board) => Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(board));
    public Task LeaveBoard(string board) => Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroup(board));
    public Task JoinPresence() => Groups.AddToGroupAsync(Context.ConnectionId, PresenceGroup);
    public Task LeavePresence() => Groups.RemoveFromGroupAsync(Context.ConnectionId, PresenceGroup);
}
