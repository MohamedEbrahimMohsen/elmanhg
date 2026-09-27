using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Integration.Health;

public sealed class HealthEndpointTests(ApiFactory factory)
{
    private const string HealthRoute = "/health";

    [Fact]
    public async Task Get_AnonymousWithDatabaseUp_Returns200Healthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(HealthRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task Get_Health_ReturnsTraceIdHeader()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(HealthRoute, TestContext.Current.CancellationToken);

        response.Headers.TryGetValues("X-Trace-Id", out var traceIds).Should().BeTrue();
        traceIds.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }
}
