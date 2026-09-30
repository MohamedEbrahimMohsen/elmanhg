using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Elmanhg.Api.Realtime;

public static class HubAccessToken
{
    public const string QueryKey = "access_token";

    // Browsers cannot set headers on WebSocket or EventSource requests, so SignalR sends the bearer token in the query; it is accepted on the hub path only, and request logs redact query values.
    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        var value = context.Request.Query[QueryKey];
        if (context.HttpContext.Request.Path.StartsWithSegments(NotificationsHub.Path) && !string.IsNullOrEmpty(value))
        {
            context.Token = value.ToString();
        }

        return Task.CompletedTask;
    }
}
