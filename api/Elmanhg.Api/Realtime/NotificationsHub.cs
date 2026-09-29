using Microsoft.AspNetCore.SignalR;

namespace Elmanhg.Api.Realtime;

public sealed class NotificationsHub : Hub
{
    public const string Path = "/api/hubs/notifications";
}
