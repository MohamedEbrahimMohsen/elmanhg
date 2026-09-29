using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Domain.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json.Serialization;

namespace Elmanhg.Api.Realtime;

public static class RealtimeExtensions
{
    public static IServiceCollection AddElmanhgRealtime(this IServiceCollection services)
    {
        services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSingleton<ITeacherThreadNotifier, SignalRTeacherThreadNotifier>();
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = HubAccessToken.OnMessageReceived;
        });
        return services;
    }

    public static IEndpointRouteBuilder MapElmanhgRealtime(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<NotificationsHub>(NotificationsHub.Path).RequireAuthorization(DefaultCodes.AuthenticatedUser);
        return endpoints;
    }
}
