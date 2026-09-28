using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Exams.MultiUnitExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class MultiUnitExamEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetOverview_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(OverviewUrl(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOverview_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(OverviewUrl(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOverview_Student_ReturnsUnitsWithServableCountsAndSizes()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [3, 4]);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await ReadOkAsync(await client.GetAsync(OverviewUrl(subjectId), CancellationToken));

        body.GetProperty("units").EnumerateArray().Select(x => (x.GetProperty("unitId").GetGuid(), x.GetProperty("name").GetString(), x.GetProperty("servableCount").GetInt32(), x.GetProperty("hasBlueprint").GetBoolean()))
            .Should().Equal((unitIds[0], "Mechanics", 3, true), (unitIds[1], "Waves", 4, true));
        body.GetProperty("sizes").EnumerateArray().Select(x => x.GetInt32()).Should().Equal(20, 40, 60);
    }

    [Fact]
    public async Task GetOverview_UnknownSubject_Returns404()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync(OverviewUrl(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Preview_TwoUnits_ReturnsMergedCounts()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await ReadOkAsync(await client.GetAsync(PreviewUrl(subjectId, unitIds, 20), CancellationToken));

        body.GetProperty("blueprint").GetProperty("typeCounts")[0].GetProperty("required").GetInt32().Should().Be(20);
        body.GetProperty("units").EnumerateArray().Select(x => x.GetProperty("questionCount").GetInt32()).Should().Equal(10, 10);
        body.GetProperty("isAvailable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Preview_OneUnit_Returns422UnitsTooFew()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync(PreviewUrl(subjectId, unitIds.Take(1), 20), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("MULTI_UNIT_EXAM_UNITS_TOO_FEW");
    }

    [Fact]
    public async Task Start_TwoUnits_StartsMultiUnitExam()
    {
        var (subjectId, unitIds, questionIds) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await ReadOkAsync(await StartMultiAsync(client, subjectId, unitIds, 20));

        body.GetProperty("kind").GetString().Should().Be("MultiUnitExam");
        var served = ItemQuestionIds(body);
        (served.Count, served.Count(questionIds[0].Contains), served.Count(questionIds[1].Contains)).Should().Be((20, 10, 10));
        body.GetProperty("items").EnumerateArray().Should().AllSatisfy(x => x.GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Null));
        var session = await ReadSessionAsync(factory, body.GetProperty("id").GetGuid());
        session.Kind.Should().Be(SessionKind.MultiUnitExam);
        session.ScopeKey.Should().StartWith("units:20:");
        session.Deadline.Should().NotBeNull();
    }

    [Fact]
    public async Task Start_SameSelectionAgain_ResumesSameSession()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (student, client) = await SignedInStudentAsync(factory);
        var first = await ReadOkAsync(await StartMultiAsync(client, subjectId, unitIds, 20));

        var second = await ReadOkAsync(await StartMultiAsync(client, subjectId, [unitIds[1], unitIds[0]], 20));

        second.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.CountAsync(x => x.StudentId == student.Id, CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Start_SizeInvalid_Returns422()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await StartMultiAsync(client, Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], 30);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("MULTI_UNIT_EXAM_SIZE_INVALID");
    }

    [Fact]
    public async Task Start_UnitFromOtherSubject_Returns404()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, otherUnitIds, _) = await SeedMultiUnitSubjectAsync(factory, [1, 1]);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await StartMultiAsync(client, subjectId, [unitIds[0], otherUnitIds[0]], 20);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task Start_UnionShort_Returns400ExamShortfall()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [5, 5]);
        var (student, client) = await SignedInStudentAsync(factory);

        using var response = await StartMultiAsync(client, subjectId, unitIds, 20);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_SHORTFALL");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.CountAsync(x => x.StudentId == student.Id, CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Start_UnitExamOpen_Returns409()
    {
        var (_, unitExamUnitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, client) = await SignedInStudentAsync(factory);
        await StartAsync(client, unitExamUnitId);

        using var response = await StartMultiAsync(client, subjectId, unitIds, 20);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("EXAM_ALREADY_IN_PROGRESS");
    }

    [Fact]
    public async Task Start_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await StartMultiAsync(client, Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], 20);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveAndSubmit_MultiUnitExam_ReturnsUnitBreakdown()
    {
        var (subjectId, unitIds, _) = await SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, client) = await SignedInStudentAsync(factory);
        var started = await ReadOkAsync(await StartMultiAsync(client, subjectId, unitIds, 20));
        var sessionId = started.GetProperty("id").GetGuid();
        using var save = await SaveAsync(client, sessionId, ItemQuestionIds(started)[0], "b");
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadOkAsync(await SubmitAsync(client, sessionId));

        body.GetProperty("submittedAt").ValueKind.Should().Be(JsonValueKind.String);
        body.GetProperty("subjectId").GetGuid().Should().Be(subjectId);
        body.GetProperty("unitBreakdown").EnumerateArray().Select(x => (x.GetProperty("unitId").GetGuid(), x.GetProperty("questionCount").GetInt32())).Should().Equal((unitIds[0], 10), (unitIds[1], 10));
    }

    private static string OverviewUrl(Guid subjectId) => $"{ExamTestData.Route}/subjects/{subjectId}/multi-unit";

    private static async Task<JsonElement> ReadOkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        }
    }
}
