using Core.Notifications.Endpoints;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Elmanhg.Tests.Core.Notifications;

public sealed class CoreNotificationEndpointsTests
{
    private const string AdminPolicy = "Probe.Admin";

    [Fact]
    public void MapCoreNotificationTemplateEndpoints_EveryEndpoint_RequiresAdminPolicy()
    {
        var endpoints = Map(app => app.MapCoreNotificationTemplateEndpoints(AdminPolicy));

        endpoints.Should().HaveCount(5).And.OnlyContain(x => x.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy == AdminPolicy));
    }

    [Fact]
    public void MapCoreFirebaseNotificationEndpoints_EveryEndpoint_RequiresAdminPolicy()
    {
        var endpoints = Map(app => app.MapCoreFirebaseNotificationEndpoints(AdminPolicy));

        endpoints.Should().HaveCount(10).And.OnlyContain(x => x.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy == AdminPolicy));
    }

    [Fact]
    public void MapCoreDevicesNotificationEndpoints_EveryEndpoint_RequiresAuthenticatedUser()
    {
        var endpoints = Map(app => app.MapCoreDevicesNotificationEndpoints());

        endpoints.Should().HaveCount(1).And.OnlyContain(x => x.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 && x.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    [Fact]
    public void MapCoreUserNotificationEndpoints_EveryEndpoint_RequiresAuthenticatedUser()
    {
        var endpoints = Map(app => app.MapCoreUserNotificationEndpoints());

        endpoints.Should().HaveCount(2).And.OnlyContain(x => x.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 && x.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    [Fact]
    public void MapCoreNotificationTemplateEndpoints_BlankPolicy_Throws()
    {
        var act = () => Map(app => app.MapCoreNotificationTemplateEndpoints(" "));

        act.Should().Throw<ArgumentException>();
    }

    private static List<RouteEndpoint> Map(Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        var app = builder.Build();
        map(app);
        return ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
    }
}
