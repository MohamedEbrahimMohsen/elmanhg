using Core.Errors;
using Core.Exceptions;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Hosting;

public sealed class ExceptionMiddlewareOrderTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_AuthorizationHandlerThrowsCoreException_ReturnsStandardErrorBody()
    {
        await using var host = CreateHost(new ForbiddenCoreException("PROBE_AUTHORIZATION_FAILED"));
        using var client = host.CreateClient();

        using var response = await client.PostAsync("/api/auth/logout", JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.Contains("X-Trace-Id").Should().BeTrue();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("code").GetString().Should().Be("PROBE_AUTHORIZATION_FAILED");
    }

    [Fact]
    public async Task Post_AuthorizationHandlerThrowsUnexpectedException_Returns500Unhandled()
    {
        await using var host = CreateHost(new InvalidOperationException("probe"));
        using var client = host.CreateClient();

        using var response = await client.PostAsync("/api/auth/logout", JsonContent.Create(new { }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("code").GetString().Should().Be(ExceptionErrorCodes.UnhandledException);
    }

    private WebApplicationFactory<Program> CreateHost(Exception exception) => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IAuthorizationHandler>(new ThrowingAuthorizationHandler(exception))));
}
