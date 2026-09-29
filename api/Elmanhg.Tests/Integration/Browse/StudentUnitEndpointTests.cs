using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Browse;

public sealed class StudentUnitEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{BrowseTestData.Route}/units/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_UnknownUnit_Returns404()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{BrowseTestData.Route}/units/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_Unit_ReturnsPublishedLessonsInOrderWithMastery()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var energy = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Energy", 3, LessonState.Published, CancellationToken);
        await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 2, LessonState.Draft, CancellationToken);
        var forces = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Forces", 1, LessonState.Published, CancellationToken);
        await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Old", 4, LessonState.Archived, CancellationToken);
        var q1 = await QuestionTestData.SeedQuestionAsync(factory, forces, approved: true, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, forces, approved: true, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, forces, new Dictionary<Guid, string> { [q1] = "b" });
        await MasteryTestData.PracticeAsync(client, forces, new Dictionary<Guid, string> { [q1] = "b" });

        var body = await BrowseTestData.GetAsync(client, $"units/{unitId}");

        body.GetProperty("subjectName").GetString().Should().Be("Physics");
        var lessons = body.GetProperty("lessons").EnumerateArray().ToList();
        lessons.Select(x => x.GetProperty("id").GetGuid()).Should().Equal(forces, energy);
        lessons[0].GetProperty("masteryPercent").GetInt32().Should().Be(50);
    }
}
