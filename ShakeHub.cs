using Microsoft.AspNetCore.SignalR;

namespace WebApplication1;

public class ShakeHub : Hub
{
    public async Task JoinRoom(string code)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        await Clients.Group(code).SendAsync("peerJoined", Context.ConnectionId);
    }

    public Task Shake(string code, double force)
        => Clients.OthersInGroup(code).SendAsync("shake", force);

    public Task Progress(string code, double value)
        => Clients.Group(code).SendAsync("progress", value);

    public Task Fell(string code)
        => Clients.Group(code).SendAsync("fell");
}