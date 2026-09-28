using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class UnitExamOverviewEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_UnitWithBlueprint_ReturnsSummaryAndAvailability()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{unitId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isAvailable").GetBoolean().Should().BeTrue();
        var typeCount = body.GetProperty("blueprint").GetProperty("typeCounts")[0];
        (typeCount.GetProperty("type").GetString(), typeCount.GetProperty("required").GetInt32(), typeCount.GetProperty("available").GetInt32()).Should().Be(("Mcq", 2, 2));
        body.GetProperty("inProgressExam").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_ShortPool_ReturnsUnavailable()
    {
        var (_, unitId, _, questionIds) = await SeedExamUnitAsync(factory, 2);
        await RetireQuestionAsync(factory, questionIds[0]);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{unitId}", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        body.GetProperty("blueprint").GetProperty("typeCounts")[0].GetProperty("available").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Get_UnknownUnit_Returns404()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{unitId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
