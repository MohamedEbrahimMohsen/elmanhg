using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using FluentAssertions;
using System.Net;
using static Elmanhg.Tests.Integration.Progress.ProgressTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Progress;

public sealed class WeakSpotsEndpointTests(ApiFactory factory)
{
    private const string WeakSpotsPath = "weak-spots";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync($"{ProgressTestData.Route}/{WeakSpotsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{ProgressTestData.Route}/{WeakSpotsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NewStudent_ReturnsEmptyLists()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await GetJsonAsync(client, WeakSpotsPath);

        (body.GetProperty("lessons").GetArrayLength(), body.GetProperty("objectives").GetArrayLength()).Should().Be((0, 0));
    }

    [Fact]
    public async Task Get_PracticedLessons_ReturnsLowestMasteryFirstAndSkipsMasteredAndUnseen()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var (lessonA, questionsA) = await SeedLessonAsync(unitId, "Forces", 1, questionCount: 2);
        var (lessonB, questionsB) = await SeedLessonAsync(unitId, "Energy", 2, questionCount: 1);
        var (lessonC, questionsC) = await SeedLessonAsync(unitId, "Momentum", 3, questionCount: 1);
        await SeedLessonAsync(unitId, "Waves", 4, questionCount: 1);
        var (_, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, lessonA, new Dictionary<Guid, string> { [questionsA[0]] = "b" });
        await MasteryTestData.PracticeAsync(client, lessonA, new Dictionary<Guid, string> { [questionsA[0]] = "b" });
        await MasteryTestData.PracticeAsync(client, lessonB, new Dictionary<Guid, string> { [questionsB[0]] = "a" });
        await MasteryTestData.PracticeAsync(client, lessonC, new Dictionary<Guid, string> { [questionsC[0]] = "b" });
        await MasteryTestData.PracticeAsync(client, lessonC, new Dictionary<Guid, string> { [questionsC[0]] = "b" });

        var body = await GetJsonAsync(client, WeakSpotsPath);

        var lessons = body.GetProperty("lessons").EnumerateArray().ToList();
        lessons.Select(x => (x.GetProperty("lessonId").GetGuid(), x.GetProperty("lessonName").GetString(), x.GetProperty("subjectName").GetString(), x.GetProperty("masteryPercent").GetInt32()))
            .Should().Equal((lessonB, "Energy", "Physics", 0), (lessonA, "Forces", "Physics", 50));
    }

    [Fact]
    public async Task Get_ObjectiveQuestionAnsweredWrong_ReturnsObjectiveWithLesson()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, CancellationToken);
        var (lessonId, objectiveId) = await SeedPublishedLessonWithObjectiveAsync(factory, unitId, "Ohm's law", 1, "State Ohm's law", CancellationToken);
        var questionId = await SeedObjectiveQuestionAsync(factory, lessonId, objectiveId, CancellationToken);
        var (masteredLessonId, masteredObjectiveId) = await SeedPublishedLessonWithObjectiveAsync(factory, unitId, "Coulomb's law", 2, "State Coulomb's law", CancellationToken);
        var masteredQuestionId = await SeedObjectiveQuestionAsync(factory, masteredLessonId, masteredObjectiveId, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [questionId] = "a" });
        await MasteryTestData.PracticeAsync(client, masteredLessonId, new Dictionary<Guid, string> { [masteredQuestionId] = "b" });
        await MasteryTestData.PracticeAsync(client, masteredLessonId, new Dictionary<Guid, string> { [masteredQuestionId] = "b" });

        var body = await GetJsonAsync(client, WeakSpotsPath);

        var objective = body.GetProperty("objectives").EnumerateArray().Should().ContainSingle().Subject;
        (objective.GetProperty("objectiveId").GetGuid(), objective.GetProperty("text").GetString(), objective.GetProperty("lessonId").GetGuid(), objective.GetProperty("lessonName").GetString(), objective.GetProperty("masteryPercent").GetInt32())
            .Should().Be((objectiveId, "State Ohm's law", lessonId, "Ohm's law", 0));
    }

    private async Task<(Guid LessonId, List<Guid> QuestionIds)> SeedLessonAsync(Guid unitId, string name, int order, int questionCount)
    {
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, name, order, LessonState.Published, CancellationToken).ConfigureAwait(false);
        List<Guid> questionIds = [];
        for (var index = 0; index < questionCount; index++)
        {
            questionIds.Add(await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false));
        }

        return (lessonId, questionIds);
    }
}
