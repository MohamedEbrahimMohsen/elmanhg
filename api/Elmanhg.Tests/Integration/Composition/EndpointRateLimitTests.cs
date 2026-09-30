using Elmanhg.Api.RateLimiting;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Composition;

public sealed class EndpointRateLimitTests(ApiFactory factory)
{
    [Fact]
    public void Endpoints_AllowAnonymousActions_MatchReviewedList()
    {
        var anonymous = AnonymousActions()
            .Select(Describe)
            .ToList();

        anonymous.Should().BeEquivalentTo(
        [
            "POST api/auth/otp/send",
            "POST api/auth/otp/verify",
            "POST api/auth/register/phone",
            "POST api/auth/register/email",
            "POST api/auth/login/phone",
            "POST api/auth/login/email",
            "POST api/auth/login/email-code",
            "POST api/auth/invitations/accept",
            "POST api/auth/refresh",
            "POST api/analytics/funnel-events",
            "POST api/client-errors",
            "POST api/payments/paymob/webhook",
            "GET api/questions/servable-count",
            "GET api/plans",
        ]);
    }

    [Fact]
    public void Endpoints_EveryAllowAnonymousAction_IsRateLimited()
    {
        var offenders = AnonymousActions()
            .Where(x => string.IsNullOrWhiteSpace(x.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName))
            .Select(Describe)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Endpoints_StudentCostlyActions_UseStudentPolicies()
    {
        var policies = ControllerActions()
            .Where(x => x.Metadata.GetMetadata<RouteNameMetadata>()?.RouteName is "SendAvatarMessage" or "CreateTeacherThread" or "FollowUpTeacherThread")
            .ToDictionary(x => x.Metadata.GetMetadata<RouteNameMetadata>()!.RouteName!, x => x.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);

        policies.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["SendAvatarMessage"] = StudentRateLimitPolicies.AvatarMessages,
            ["CreateTeacherThread"] = StudentRateLimitPolicies.AskTeacherSubmissions,
            ["FollowUpTeacherThread"] = StudentRateLimitPolicies.AskTeacherSubmissions,
        });
    }

    private List<RouteEndpoint> ControllerActions()
    {
        return factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(x => x.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();
    }

    private List<RouteEndpoint> AnonymousActions() => [.. ControllerActions().Where(x => x.Metadata.GetMetadata<IAllowAnonymous>() is not null)];

    private static string Describe(RouteEndpoint endpoint) => $"{string.Join(",", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}";
}
