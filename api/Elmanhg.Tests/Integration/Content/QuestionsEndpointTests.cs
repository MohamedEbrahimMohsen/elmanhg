using Elmanhg.Domain.Questions;
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
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.Content;

public sealed class QuestionsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";

    [Fact]
    public async Task Post_AdminMcq_CreatesPendingVersionOneWithRevisionAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, McqRequest(lessonId), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(1);
        question.SubjectId.Should().Be(subjectId);
        question.Revisions.Should().ContainSingle();
        question.Tags.Should().Equal("arithmetic");
        (await ContentTestData.ReadAuditAsync(factory, "Question.Create", id, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Theory]
    [InlineData("Mcq", "<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""")]
    [InlineData("Multi", "<p>Vectors?</p>", """{"options":[{"id":"a","text":"Force"},{"id":"b","text":"Mass"}]}""", """{"correctOptionIds":["a"]}""")]
    [InlineData("TrueFalse", "<p>Mass is a vector.</p>", "{}", """{"correctAnswer":false}""")]
    [InlineData("Fill", "<p>v = [[1]] m/s</p>", """{"blanks":[{"id":"1"}]}""", """{"blanks":[{"id":"1","acceptedAnswers":["20"]}]}""")]
    [InlineData("Short", "<p>g = ?</p>", """{"answerKind":"numeric"}""", """{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""")]
    public async Task Post_EachV1Type_Returns200(string type, string stem, string body, string gradingSpec)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, new { lessonId, type, stem, body = Json(body), gradingSpec = Json(gradingSpec), difficulty = "Easy", tags = Array.Empty<string>(), maxScore = 2 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        (await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken)).Type.Should().Be(Enum.Parse<QuestionType>(type));
    }

    [Fact]
    public async Task Post_BodyClaimsApproved_StillPending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var request = new { lessonId, type = "Mcq", stem = "<p>2 + 2 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), difficulty = "Medium", maxScore = 1, validationStatus = "Approved" };

        using var response = await admin.PostAsJsonAsync(Route, request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        (await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task Post_McqCorrectOptionUnknown_Returns422QuestionCorrectOptionInvalid()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var request = new { lessonId, type = "Mcq", stem = "<p>2 + 2 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"z"}"""), difficulty = "Medium", maxScore = 1 };

        using var response = await admin.PostAsJsonAsync(Route, request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_CORRECT_OPTION_INVALID");
    }

    [Fact]
    public async Task Post_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, McqRequest(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Post_ObjectiveOfOtherLesson_Returns400QuestionObjectiveNotInLesson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var (_, otherLessonId) = await SeedLessonAsync();
        var otherObjectiveId = (await ContentTestData.ReadLessonAsync(factory, otherLessonId, cancellationToken)).Objectives[0].Id;
        using var admin = await AdminClientAsync();
        var request = new { lessonId, type = "Mcq", stem = "<p>2 + 2 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), difficulty = "Medium", objectiveId = otherObjectiveId, maxScore = 1 };

        using var response = await admin.PostAsJsonAsync(Route, request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_OBJECTIVE_NOT_IN_LESSON");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var (subjectId, lessonId) = await SeedLessonAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsJsonAsync(Route, McqRequest(lessonId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(Route, McqRequest(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ContentEditOnApproved_ResetsPendingBumpsVersionAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{questionId}", McqRequest(lessonId, "<p>3 + 3 = ?</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(2);
        question.Revisions.Should().HaveCount(2);
        question.ValidatedBy.Should().BeNull();
        var audit = await ContentTestData.ReadAuditAsync(factory, "Question.Update", questionId, cancellationToken);
        audit.Diff.Should().Contain("\"version\"");
    }

    [Fact]
    public async Task Put_DifficultyOnlyOnApproved_KeepsApprovedAndVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();
        var request = new { type = "Mcq", stem = "<p>2 + 2 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), explanation = "<p>Add the numbers.</p>", difficulty = "Hard", maxScore = 1 };

        using var response = await admin.PutAsJsonAsync($"{Route}/{questionId}", request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        question.Version.Should().Be(1);
        question.Difficulty.Should().Be(QuestionDifficulty.Hard);
    }

    [Fact]
    public async Task Put_TypeChanged_Returns400QuestionTypeImmutable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var admin = await AdminClientAsync();
        var request = new { type = "Multi", stem = "<p>2 + 2 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionIds":["b"]}"""), difficulty = "Medium", maxScore = 1 };

        using var response = await admin.PutAsJsonAsync($"{Route}/{questionId}", request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_TYPE_IMMUTABLE");
    }

    [Fact]
    public async Task Put_UnknownQuestion_Returns404QuestionNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", McqRequest(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task Put_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route}/{questionId}", McqRequest(lessonId, "<p>Changed</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        (question.Version, question.Stem).Should().Be((1, "<p>2 + 2 = ?</p>"));
    }

    [Fact]
    public async Task Get_Admin_ReturnsDetailWithRawJson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{questionId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("body").GetProperty("options").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("gradingSpec").GetProperty("correctOptionId").GetString().Should().Be("b");
        body.GetProperty("validationStatus").GetString().Should().Be("Pending");
        body.GetProperty("version").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Get_UnknownQuestion_Returns404QuestionNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task Get_Student_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, cancellationToken);

        using var response = await client.GetAsync($"{Route}/{questionId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static object McqRequest(Guid lessonId, string stem = "<p>2 + 2 = ?</p>")
    {
        return new { lessonId, type = "Mcq", stem, body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), explanation = "<p>Add the numbers.</p>", difficulty = "Medium", maxScore = 1, tags = new[] { "arithmetic" } };
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law"], TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId);
    }

    private async Task<int> CountQuestionsAsync(Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Questions.CountAsync(x => x.LessonId == lessonId, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> TeacherClientAsync(Guid assignedSubjectId)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, assignedSubjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
