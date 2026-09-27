using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Composition;

public sealed class EndpointAuthorizationTests(ApiFactory factory)
{
    [Fact]
    public void Endpoints_EveryControllerAction_DeclaresPolicyOrAllowAnonymous()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(x => x.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();

        var unprotected = endpoints
            .Where(x => x.Metadata.GetMetadata<IAllowAnonymous>() is null && !x.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => !string.IsNullOrWhiteSpace(data.Policy)))
            .Select(x => x.DisplayName)
            .ToList();

        endpoints.Should().NotBeEmpty();
        unprotected.Should().BeEmpty();
    }
}
