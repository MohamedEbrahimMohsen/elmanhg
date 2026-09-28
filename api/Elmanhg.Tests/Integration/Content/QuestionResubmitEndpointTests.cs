using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.Content;

public sealed class QuestionResubmitEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";
    private const string Reason = "Wrong unit conversion";

    [Fact]
    public async Task Put_RejectedWithEdit_ReturnsPendingVersionTwoAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var questionId = await SeedRejectedAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{questionId}/resubmit", McqRequest(Guid.Empty, "<p>3 + 3 = ?</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(2);
        question.RejectionReason.Should().BeNull();
        question.ValidatedBy.Should().BeNull();
        (await ContentTestData.ReadAuditAsync(factory, "Question.Resubmit", questionId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Put_PendingQuestion_Returns400QuestionNotRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{questionId}/resubmit", McqRequest(lessonId, "<p>3 + 3 = ?</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_REJECTED");
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        (question.ValidationStatus, question.Version, question.Stem).Should().Be((QuestionValidationStatus.Pending, 1, "<p>2 + 2 = ?</p>"));
    }

    [Fact]
    public async Task Put_UnknownQuestion_Returns404QuestionNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}/resubmit", McqRequest(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task Put_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, lessonId) = await SeedLessonAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var questionId = await QuestionTestData.SeedRejectedQuestionAsync(factory, lessonId, teacher, Reason, cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route}/{questionId}/resubmit", McqRequest(lessonId, "<p>Changed</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
    }

    [Fact]
    public async Task Get_RejectedQuestion_ReturnsRejectionReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var questionId = await SeedRejectedAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{questionId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("rejectionReason").GetString().Should().Be(Reason);
        body.GetProperty("validationStatus").GetString().Should().Be("Rejected");
    }

    private async Task<Guid> SeedRejectedAsync()
    {
        var (_, lessonId) = await SeedLessonAsync().ConfigureAwait(false);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await QuestionTestData.SeedRejectedQuestionAsync(factory, lessonId, teacher, Reason, TestContext.Current.CancellationToken).ConfigureAwait(false);
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
