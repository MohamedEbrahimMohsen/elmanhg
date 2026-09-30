using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class AccessTokenRevocationTests(ApiFactory factory)
{
    private const string UsersRoute = "/api/users";
    private const string StudentRoute = "/api/progress/subjects";

    [Fact]
    public async Task Get_AccessTokenIssuedBeforeSuspension_Returns401AfterReactivation()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        using var staleClient = await ScopeTestData.SignedInClientAsync(factory, student, TestContext.Current.CancellationToken);
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var adminClient = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);
        using (var suspend = await adminClient.PostAsync($"{UsersRoute}/{student.Id}/suspend", null, TestContext.Current.CancellationToken))
        {
            suspend.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var reactivate = await adminClient.PostAsync($"{UsersRoute}/{student.Id}/reactivate", null, TestContext.Current.CancellationToken))
        {
            reactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var stale = await staleClient.GetAsync(StudentRoute, TestContext.Current.CancellationToken);

        stale.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var freshClient = await ScopeTestData.SignedInClientAsync(factory, student, TestContext.Current.CancellationToken);
        using var fresh = await freshClient.GetAsync(StudentRoute, TestContext.Current.CancellationToken);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AccessTokenAfterSignOut_Returns401()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, TestContext.Current.CancellationToken);
        using (var warm = await client.GetAsync(StudentRoute, TestContext.Current.CancellationToken))
        {
            warm.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var logout = await client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken))
        {
            logout.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await client.GetAsync(StudentRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
