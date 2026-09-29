using Elmanhg.Application.Shared.Observability;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Observability;

public sealed class ClientErrorsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/client-errors";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_AnonymousReport_Returns200AndRecordsMetric()
    {
        using var errors = new MetricCollector<long>(MeterFactory(), ElmanhgTelemetry.SourceName, "elmanhg.client.errors");
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsJsonAsync(Route, new { message = "TypeError: x is undefined", errorName = "TypeError", stack = "at App (main.js:1:1)", source = "Route", path = "/student" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        errors.GetMeasurementSnapshot().Where(x => Equals(x.Tags[ElmanhgMetrics.SourceTag], "Route")).Sum(x => x.Value).Should().Be(1);
    }

    [Fact]
    public async Task Post_SignedInStudent_Returns200()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.PostAsJsonAsync(Route, new { message = "boom", source = "Window", path = "/student" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_EmptyMessage_Returns422AndRecordsValidationFailed()
    {
        using var requests = new MetricCollector<long>(MeterFactory(), ElmanhgTelemetry.SourceName, "elmanhg.requests");
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsJsonAsync(Route, new { message = "", source = "Window" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("CLIENT_ERROR_MESSAGE_REQUIRED");
        requests.GetMeasurementSnapshot().Should().Contain(x => Equals(x.Tags[ElmanhgMetrics.RequestTag], "ReportClientErrorCommand") && Equals(x.Tags[ElmanhgMetrics.OutcomeTag], "VALIDATION_FAILED"));
    }

    [Fact]
    public async Task Post_OverIpLimit_Returns429TooManyRequests()
    {
        await using var limitedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ClientErrors:PermitLimit"] = "1" })));
        using var client = limitedFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var first = await client.PostAsJsonAsync(Route, new { message = "first", source = "Window" }, CancellationToken);

        using var response = await client.PostAsJsonAsync(Route, new { message = "second", source = "Window" }, CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(response)).Should().Be("TOO_MANY_REQUESTS");
    }

    private IMeterFactory MeterFactory() => factory.Services.GetRequiredService<IMeterFactory>();

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
