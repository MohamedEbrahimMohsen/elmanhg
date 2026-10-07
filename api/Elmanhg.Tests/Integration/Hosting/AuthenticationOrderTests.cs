using Core.Exceptions;
using Elmanhg.Application.Users.CheckUserActive;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Hosting;

public sealed class AuthenticationOrderTests(ApiFactory factory)
{
    private const string StudentRoute = "/api/progress/subjects";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ActiveUserCheckThrows_Returns500StandardErrorBodyInsideRequestLogging()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var signedIn = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestHandler<CheckUserActiveQuery, bool>>();
            services.AddTransient<IRequestHandler<CheckUserActiveQuery, bool>, ThrowingCheckUserActiveHandler>();
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;

        using var response = await client.GetAsync(StudentRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.Contains("X-Trace-Id").Should().BeTrue();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        body.RootElement.GetProperty("code").GetString().Should().Be(ExceptionErrorCodes.UnhandledException);
    }

    [Fact]
    public async Task Get_ValidTokenAfterExplicitAuthentication_ReachesStudentEndpoint()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync(StudentRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
