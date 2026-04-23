using Microsoft.AspNetCore.SignalR;

namespace TodoApi.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        // This broadcasts the message to all connected clients
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
