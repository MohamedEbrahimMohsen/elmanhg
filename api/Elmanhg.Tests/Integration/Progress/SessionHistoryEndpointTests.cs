using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Progress.ProgressTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Progress;

public sealed class SessionHistoryEndpointTests(ApiFactory factory)
{
    private const string SessionsPath = "sessions";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync($"{ProgressTestData.Route}/{SessionsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{ProgressTestData.Route}/{SessionsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_PageSizeAboveMax_Returns422WithCode()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ProgressTestData.Route}/{SessionsPath}?pageSize=51", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("SESSION_HISTORY_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task Get_KindOutOfRange_Returns400ModelBindingError()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ProgressTestData.Route}/{SessionsPath}?kind=9", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("errors").TryGetProperty("kind", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_Sessions_ReturnsNewestFirstWithLessonNamesAndOpenState()
    {
        var unitId = await SeedUnitAsync("Mechanics");
        var (lessonA, _) = await SeedLessonAsync(unitId, "Forces", 1);
        var (lessonB, _) = await SeedLessonAsync(unitId, "Energy", 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var finishedId = await MasteryTestData.PracticeAsync(client, lessonA, new Dictionary<Guid, string>());
        var openId = (await StartQuizAsync(client, lessonB)).GetProperty("id").GetGuid();

        var body = await GetJsonAsync(client, SessionsPath);

        body.GetProperty("totalItems").GetInt64().Should().Be(2);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Select(x => (x.GetProperty("id").GetGuid(), x.GetProperty("kind").GetString(), x.GetProperty("lessonId").GetGuid(), x.GetProperty("scopeName").GetString()))
            .Should().Equal((openId, "Quiz", lessonB, "Energy"), (finishedId, "Quiz", lessonA, "Forces"));
        (items[0].GetProperty("submittedAt").ValueKind, items[0].GetProperty("scorePercent").ValueKind).Should().Be((JsonValueKind.Null, JsonValueKind.Null));
        (items[1].GetProperty("submittedAt").ValueKind, items[1].GetProperty("scorePercent").ValueKind).Should().Be((JsonValueKind.String, JsonValueKind.Number));
    }

    [Fact]
    public async Task Get_KindExam_ReturnsOnlyExamsWithUnitName()
    {
        var unitId = await SeedUnitAsync("Optics");
        var (lessonId, _) = await SeedLessonAsync(unitId, "Lenses", 1);
        var (student, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, lessonId, new Dictionary<Guid, string>());
        var examId = await InsertUnitExamSessionAsync(factory, student.Id, unitId, 75m, submitted: true, isTestMode: false, new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));

        var exams = await GetJsonAsync(client, $"{SessionsPath}?kind=Exam");
        var quizzes = await GetJsonAsync(client, $"{SessionsPath}?kind=Quiz");

        var exam = exams.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        (exam.GetProperty("id").GetGuid(), exam.GetProperty("kind").GetString(), exam.GetProperty("unitId").GetGuid(), exam.GetProperty("scopeName").GetString(), exam.GetProperty("scorePercent").GetDecimal())
            .Should().Be((examId, "UnitExam", unitId, "Optics", 75m));
        quizzes.GetProperty("items").EnumerateArray().Should().ContainSingle().Which.GetProperty("kind").GetString().Should().Be("Quiz");
    }

    [Fact]
    public async Task Get_OtherStudentsSessions_AreNotReturned()
    {
        var unitId = await SeedUnitAsync("Mechanics");
        var (lessonId, _) = await SeedLessonAsync(unitId, "Forces", 1);
        var (_, first) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(first, lessonId, new Dictionary<Guid, string>());
        var (_, second) = await SignedInStudentAsync(factory);

        var body = await GetJsonAsync(second, SessionsPath);

        body.GetProperty("totalItems").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Get_SecondPage_ReturnsRemainingItem()
    {
        var unitId = await SeedUnitAsync("Mechanics");
        var (lessonA, _) = await SeedLessonAsync(unitId, "Forces", 1);
        var (lessonB, _) = await SeedLessonAsync(unitId, "Energy", 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var olderId = await MasteryTestData.PracticeAsync(client, lessonA, new Dictionary<Guid, string>());
        await MasteryTestData.PracticeAsync(client, lessonB, new Dictionary<Guid, string>());

        var body = await GetJsonAsync(client, $"{SessionsPath}?pageSize=1&pageNumber=2");

        body.GetProperty("items").EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(olderId);
        body.GetProperty("totalPages").GetInt64().Should().Be(2);
    }

    private async Task<Guid> SeedUnitAsync(string name)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        return await ContentTestData.SeedUnitAsync(factory, subjectId, name, 1, CancellationToken).ConfigureAwait(false);
    }

    private async Task<(Guid LessonId, Guid QuestionId)> SeedLessonAsync(Guid unitId, string name, int order)
    {
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, name, order, LessonState.Published, CancellationToken).ConfigureAwait(false);
        return (lessonId, await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false));
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("code").GetString();
}
