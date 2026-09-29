using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;
using static Elmanhg.Tests.Integration.TeacherThreads.TeacherThreadTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class CreateTeacherThreadEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_LessonContextWithImage_PersistsThreadAndServesImage()
    {
        var (subjectId, lessonId) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why is F = ma?", lessonId: lessonId, imageFileName: "photo.png"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("isOverdue").GetBoolean()).Should().Be(("Open", false));
        var context = body.GetProperty("context");
        (context.GetProperty("subjectName").GetString(), context.GetProperty("unitName").GetString(), context.GetProperty("lessonName").GetString()).Should().Be(("Physics", "Mechanics", "Newton's laws"));
        var imageUrl = body.GetProperty("messages")[0].GetProperty("imageUrl").GetString();
        imageUrl.Should().StartWith("/api/media/teacher-threads/").And.EndWith(".png");
        using var served = await client.GetAsync(imageUrl, CancellationToken);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync(CancellationToken)).Should().Equal(PngBytes);
        served.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        var thread = (await ReadThreadsAsync(factory, student.Id)).Should().ContainSingle().Subject;
        (thread.SubjectId, thread.SlaDueAt - thread.SubmittedAt).Should().Be((subjectId, TimeSpan.FromHours(24)));
        thread.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_AttemptContext_SnapshotsQuestionStemAndAttemptId()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 5);
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);
        var attemptId = await AnswerFirstQuestionAsync(client, lessonId);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why is my answer wrong?", attemptId: attemptId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var context = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("context");
        context.GetProperty("attemptId").GetGuid().Should().Be(attemptId);
        context.GetProperty("questionId").ValueKind.Should().Be(JsonValueKind.String);
        context.GetProperty("questionStem").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Post_BaseWithoutAddOn_Returns403AskTeacherRequiresSubscription()
    {
        var (_, lessonId) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: lessonId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("ASK_TEACHER_REQUIRES_SUBSCRIPTION");
        (await ReadThreadsAsync(factory, student.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_MonthlyQuotaUsed_Returns403AskTeacherMonthlyLimitReached()
    {
        var (subjectId, lessonId) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);
        await SeedThreadsAsync(factory, student.Id, subjectId, 20, DateTimeOffset.UtcNow.AddMinutes(-1));

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: lessonId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("ASK_TEACHER_MONTHLY_LIMIT_REACHED");
        (await ReadThreadsAsync(factory, student.Id)).Should().HaveCount(20);
    }

    [Fact]
    public async Task Post_OtherStudentsAttempt_Returns404AttemptNotFound()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 5);
        var (_, other) = await SignedInAskTeacherStudentAsync(factory);
        var attemptId = await AnswerFirstQuestionAsync(other, lessonId);
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", attemptId: attemptId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("ATTEMPT_NOT_FOUND");
    }

    [Fact]
    public async Task Post_TwoContexts_Returns422TeacherThreadContextInvalid()
    {
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: Guid.NewGuid(), attemptId: Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_CONTEXT_INVALID");
    }

    [Fact]
    public async Task Post_SvgImage_Returns422TeacherThreadImageTypeInvalid()
    {
        var (_, lessonId) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: lessonId, imageFileName: "photo.svg", imageContentType: "image/svg+xml"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_IMAGE_TYPE_INVALID");
        (await ReadThreadsAsync(factory, student.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_HtmlSpoofedAsPng_Returns422TeacherThreadImageTypeInvalid()
    {
        var (_, lessonId) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: lessonId, imageFileName: "photo.png", imageContentType: "image/png", imageBytes: "<html><script>alert(1)</script></html>"u8.ToArray()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_IMAGE_TYPE_INVALID");
        (await ReadThreadsAsync(factory, student.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync(TeacherThreadTestData.Route, QuestionForm("Why?", lessonId: Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<Guid> AnswerFirstQuestionAsync(HttpClient client, Guid lessonId)
    {
        var session = await StartQuizAsync(client, lessonId, 5);
        var item = session.GetProperty("items")[0];
        using var answer = await AnswerAsync(client, session.GetProperty("id").GetGuid(), item.GetProperty("questionId").GetGuid(), "a");
        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await answer.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("attempt").GetProperty("id").GetGuid();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
