using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.QuestionValidation;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class DashboardAccessEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static TheoryData<string> Cards => ["students", "subscribers", "content", "solve-rate", "success-rate", "validation", "ask-teacher", "payments", "funnel"];

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task Get_Anonymous_Returns401(string card)
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{Route}/{card}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Teacher)]
    public async Task Get_NonAdmin_Returns403(UserRole role)
    {
        var user = role == UserRole.Student ? await ScopeTestData.SeedStudentAsync(factory, CancellationToken) : await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, user, CancellationToken);

        using var response = await client.GetAsync($"{Route}/students", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task Get_Admin_Returns200ForEveryCard(string card)
    {
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, card);

        body.GetProperty("generatedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Get_FromAfterTo_Returns422DashboardDateRangeInvalid()
    {
        using var client = await AdminClientAsync(factory);

        using var response = await client.GetAsync($"{Route}/students?from=2026-01-10&to=2026-01-05", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ValidationTestData.ReadCodeAsync(response)).Should().Contain("DASHBOARD_DATE_RANGE_INVALID");
    }

    [Fact]
    public async Task Get_RangeTooWide_Returns422DashboardDateRangeTooWide()
    {
        using var client = await AdminClientAsync(factory);

        using var response = await client.GetAsync($"{Route}/payments?from=2020-01-01&to=2021-12-31", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ValidationTestData.ReadCodeAsync(response)).Should().Contain("DASHBOARD_DATE_RANGE_TOO_WIDE");
    }

    [Fact]
    public async Task Get_UnknownSubject_Returns404SubjectNotFound()
    {
        using var client = await AdminClientAsync(factory);

        using var response = await client.GetAsync($"{Route}/content?subjectId={Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBJECT_NOT_FOUND");
    }
}
