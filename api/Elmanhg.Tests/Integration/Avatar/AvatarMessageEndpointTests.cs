using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.ContentRetrieval;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Avatar;

[Collection(ContentRetrievalCollection.Name)]
public sealed class AvatarMessageEndpointTests(ApiFactory factory)
{
    private const string Explanation = "<h2>قانون أوم</h2><p>شدة التيار تتناسب طرديا مع فرق الجهد عند ثبوت درجة الحرارة</p>";
    private const string Summary = "<p>الخلية النباتية لها جدار خلوي</p>";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostMessage_LessonEntryIndexed_ReturnsReplyWithCitationAndRecordsUsage()
    {
        var lessonId = await ContentRetrievalTestData.SeedPublishedLessonAsync(factory, Explanation, Summary, [], CancellationToken);
        await ContentRetrievalTestData.ReindexAsync(factory, lessonId, CancellationToken);
        var (student, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, new { entryPoint = "Lesson", lessonId, history = Array.Empty<object>(), message = "شدة التيار تتناسب طرديا مع فرق الجهد" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("reply").GetString().Should().Be(FakeAiServiceClient.FakeReply);
        var citation = body.GetProperty("citations")[0];
        citation.GetProperty("reference").GetString().Should().Be("explanation-1");
        citation.GetProperty("section").GetString().Should().Be("Explanation");
        citation.GetProperty("lessonId").GetGuid().Should().Be(lessonId);
        body.GetProperty("messagesUsedToday").GetInt32().Should().Be(1);
        (await ReadUsageAsync(factory, student.Id)).Should().ContainSingle().Which.EntryPoint.Should().Be(AvatarEntryPoint.Lesson);
    }

    [Fact]
    public async Task PostMessage_QuizQuestionAnswered_Returns200()
    {
        var (session, questionId, student, client) = await SeedQuizAsync(answer: true);

        using var response = await PostMessageAsync(client, QuestionBody("QuizQuestion", session, questionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadUsageAsync(factory, student.Id)).Should().ContainSingle().Which.EntryPoint.Should().Be(AvatarEntryPoint.QuizQuestion);
    }

    [Fact]
    public async Task PostMessage_QuizQuestionUnanswered_Returns400QuestionNotAnswered()
    {
        var (session, questionId, student, client) = await SeedQuizAsync(answer: false);

        using var response = await PostMessageAsync(client, QuestionBody("QuizQuestion", session, questionId));

        await ExpectProblemAsync(response, HttpStatusCode.BadRequest, "AVATAR_QUESTION_NOT_ANSWERED");
        (await ReadUsageAsync(factory, student.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task PostMessage_OtherStudentsQuizSession_Returns404SessionNotFound()
    {
        var (session, questionId, _, _) = await SeedQuizAsync(answer: true);
        var (_, other) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(other, QuestionBody("QuizQuestion", session, questionId));

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task PostMessage_LessonEntryWithUnansweredOpenQuiz_WithholdsItsQuestionChunks()
    {
        var (lessonId, _, _, client) = await SeedIndexedQuizAsync(answer: false);

        using var response = await PostMessageAsync(client, LessonBody(lessonId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PostMessage_QuizQuestionAnswered_CitesOnlyTheAnsweredQuestionChunk()
    {
        var (_, sessionId, questionId, client) = await SeedIndexedQuizAsync(answer: true);

        using var response = await PostMessageAsync(client, QuestionBody("QuizQuestion", sessionId, questionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var citation = body.GetProperty("citations").EnumerateArray().Should().ContainSingle().Subject;
        citation.GetProperty("section").GetString().Should().Be("QuestionExplanation");
        citation.GetProperty("questionId").GetGuid().Should().Be(questionId);
    }

    [Fact]
    public async Task PostMessage_ExamReviewSubmittedExam_Returns200()
    {
        var (_, unitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var exam = await ExamTestData.StartAsync(client, unitId);
        var sessionId = exam.GetProperty("id").GetGuid();
        var questionIds = ExamTestData.ItemQuestionIds(exam);
        (await ExamTestData.SaveAsync(client, sessionId, questionIds[0], "a")).EnsureSuccessStatusCode();
        (await ExamTestData.SubmitAsync(client, sessionId)).EnsureSuccessStatusCode();

        using var response = await PostMessageAsync(client, QuestionBody("ExamReview", sessionId, questionIds[1]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostMessage_StudentWithOpenExam_Returns403ExamInProgressAndRecordsNothing()
    {
        var (_, unitId, lessonId, _) = await ExamTestData.SeedExamUnitAsync(factory, 2);
        var (student, client) = await SignedInStudentAsync(factory);
        await ExamTestData.StartAsync(client, unitId);

        using var response = await PostMessageAsync(client, LessonBody(lessonId));

        await ExpectProblemAsync(response, HttpStatusCode.Forbidden, "AVATAR_EXAM_IN_PROGRESS");
        (await ReadUsageAsync(factory, student.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task PostMessage_FreeStudentAtDailyLimit_Returns403AndRecordsNothing()
    {
        var lessonId = await ContentRetrievalTestData.SeedPublishedLessonAsync(factory, Explanation, Summary, [], CancellationToken);
        var (student, client) = await SignedInFreeStudentAsync(factory);
        await SeedUsageAsync(factory, student.Id, 5, DateTimeOffset.UtcNow);

        using var response = await PostMessageAsync(client, LessonBody(lessonId));

        await ExpectProblemAsync(response, HttpStatusCode.Forbidden, "AVATAR_DAILY_LIMIT_REACHED");
        (await ReadUsageAsync(factory, student.Id)).Should().HaveCount(5);
    }

    [Fact]
    public async Task PostMessage_FreeStudentLockedLesson_Returns403LessonLocked()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, CancellationToken);
        await ContentTestData.SeedLessonInStateAsync(factory, unitId, "First", 1, LessonState.Published, CancellationToken);
        var secondId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Second", 2, LessonState.Published, CancellationToken);
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, LessonBody(secondId));

        await ExpectProblemAsync(response, HttpStatusCode.Forbidden, "LESSON_LOCKED");
    }

    [Fact]
    public async Task PostMessage_DraftLesson_Returns404LessonNotFound()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, CancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 1, LessonState.Draft, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await PostMessageAsync(client, LessonBody(lessonId));

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task PostMessage_GlobalEntry_Returns200WithoutCitations()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, new { entryPoint = "Global", history = Array.Empty<object>(), message = "كيف أذاكر الفيزياء؟" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PostMessage_BlankMessage_Returns422AvatarMessageRequired()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, new { entryPoint = "Global", history = Array.Empty<object>(), message = "  " });

        await ExpectProblemAsync(response, HttpStatusCode.UnprocessableEntity, "AVATAR_MESSAGE_REQUIRED");
    }

    [Fact]
    public async Task PostMessage_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await PostMessageAsync(client, new { entryPoint = "Global", history = Array.Empty<object>(), message = "q" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostMessage_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await PostMessageAsync(anonymous, new { entryPoint = "Global", history = Array.Empty<object>(), message = "q" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static object LessonBody(Guid lessonId) => new { entryPoint = "Lesson", lessonId, history = Array.Empty<object>(), message = "ما هو قانون أوم؟" };

    private static object QuestionBody(string entryPoint, Guid sessionId, Guid questionId) => new { entryPoint, sessionId, questionId, history = Array.Empty<object>(), message = "لماذا إجابتي خطأ؟" };

    private async Task<(Guid SessionId, Guid QuestionId, User Student, HttpClient Client)> SeedQuizAsync(bool answer)
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 5).ConfigureAwait(false);
        var (student, client) = await SignedInFreeStudentAsync(factory).ConfigureAwait(false);
        var session = await StartQuizAsync(client, lessonId, 5).ConfigureAwait(false);
        var sessionId = session.GetProperty("id").GetGuid();
        var questionId = session.GetProperty("items")[0].GetProperty("questionId").GetGuid();
        if (answer)
        {
            using var answered = await AnswerAsync(client, sessionId, questionId, "a").ConfigureAwait(false);
            answered.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return (sessionId, questionId, student, client);
    }

    private async Task<(Guid LessonId, Guid SessionId, Guid QuestionId, HttpClient Client)> SeedIndexedQuizAsync(bool answer)
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 5).ConfigureAwait(false);
        await ContentRetrievalTestData.ReindexAsync(factory, lessonId, CancellationToken).ConfigureAwait(false);
        var (_, client) = await SignedInFreeStudentAsync(factory).ConfigureAwait(false);
        var session = await StartQuizAsync(client, lessonId, 5).ConfigureAwait(false);
        var sessionId = session.GetProperty("id").GetGuid();
        var questionId = session.GetProperty("items")[0].GetProperty("questionId").GetGuid();
        if (answer)
        {
            using var answered = await AnswerAsync(client, sessionId, questionId, "a").ConfigureAwait(false);
            answered.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return (lessonId, sessionId, questionId, client);
    }

    private static async Task ExpectProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        (await ExamTestData.ReadCodeAsync(response).ConfigureAwait(false)).Should().Be(code);
    }
}
