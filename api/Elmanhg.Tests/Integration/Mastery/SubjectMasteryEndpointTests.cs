using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Mastery;

public sealed class SubjectMasteryEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(SubjectRoute(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(SubjectRoute(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_UnknownSubject_Returns404()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync(SubjectRoute(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_SubjectWithProgress_ReturnsUnitAndLessonPercentages()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var lessonA = await SeedLessonWithQuestionsAsync(unitId, "Forces", 1, LessonState.Published);
        var lessonB = await SeedLessonWithQuestionsAsync(unitId, "Energy", 2, LessonState.Published);
        await SeedLessonWithQuestionsAsync(unitId, "Waves", 3, LessonState.Draft);
        var (_, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, lessonA.LessonId, new Dictionary<Guid, string> { [lessonA.Q1] = "b" });
        await MasteryTestData.PracticeAsync(client, lessonA.LessonId, new Dictionary<Guid, string> { [lessonA.Q1] = "b" });

        var body = await GetSubjectAsync(client, subjectId);

        AssertTotals(body, servable: 4, mastered: 1, percent: 25);
        var unit = body.GetProperty("units").EnumerateArray().Should().ContainSingle().Subject;
        AssertTotals(unit, servable: 4, mastered: 1, percent: 25);
        var lessons = unit.GetProperty("lessons").EnumerateArray().ToList();
        lessons.Select(x => x.GetProperty("lessonId").GetGuid()).Should().Equal(lessonA.LessonId, lessonB.LessonId);
        AssertTotals(lessons[0], servable: 2, mastered: 1, percent: 50);
        lessons[0].GetProperty("seenCount").GetInt32().Should().Be(1);
        AssertTotals(lessons[1], servable: 2, mastered: 0, percent: 0);
    }

    [Fact]
    public async Task Get_SubjectWithoutLessons_ReturnsZeroTotals()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Chemistry", 1, CancellationToken);
        await ContentTestData.SeedUnitAsync(factory, subjectId, "Atoms", 1, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await GetSubjectAsync(client, subjectId);

        AssertTotals(body, servable: 0, mastered: 0, percent: 0);
        body.GetProperty("seenCount").GetInt32().Should().Be(0);
        body.GetProperty("units").EnumerateArray().Should().ContainSingle().Which.GetProperty("lessons").GetArrayLength().Should().Be(0);
    }

    private static string SubjectRoute(Guid subjectId) => $"{MasteryTestData.Route}/subjects/{subjectId}";

    private static async Task<JsonElement> GetSubjectAsync(HttpClient client, Guid subjectId)
    {
        using var response = await client.GetAsync(SubjectRoute(subjectId), CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    private async Task<(Guid LessonId, Guid Q1)> SeedLessonWithQuestionsAsync(Guid unitId, string name, int order, LessonState state)
    {
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, name, order, state, CancellationToken).ConfigureAwait(false);
        var q1 = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false);
        return (lessonId, q1);
    }

    private static void AssertTotals(JsonElement element, int servable, int mastered, int percent)
    {
        (element.GetProperty("servableCount").GetInt32(), element.GetProperty("masteredCount").GetInt32(), element.GetProperty("masteryPercent").GetInt32()).Should().Be((servable, mastered, percent));
    }
}
