using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using static Elmanhg.Tests.Integration.QuestionValidation.ValidationTestData;

namespace Elmanhg.Tests.Integration.QuestionValidation;

public sealed class QuestionDecisionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Approve_AssignedTeacher_ApprovesStampsValidatorAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, teacherId, client) = await SeedPendingAsync();

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        question.ValidatedBy.Should().Be(teacherId);
        question.Decisions.Should().ContainSingle().Which.Outcome.Should().Be(QuestionDecisionOutcome.Approved);
        (await ContentTestData.ReadAuditAsync(factory, "Question.Approve", questionId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Approve_WithDifficulty_ChangesDifficultyWithoutVersionBump()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, client) = await SeedPendingAsync();

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 1, difficulty = "Hard" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.Difficulty.Should().Be(QuestionDifficulty.Hard);
        question.Version.Should().Be(1);
    }

    [Fact]
    public async Task Approve_StaleVersion_Returns409QuestionVersionChanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, client) = await SeedPendingAsync();

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 2 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_VERSION_CHANGED");
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task Approve_OtherSubject_Returns403SubjectOutOfScope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, _) = await SeedPendingAsync();
        var other = await SeedSubjectTreeAsync(factory, "Math");
        var (_, client) = await SeedAssignedTeacherAsync(factory, other.SubjectId);

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task Approve_AsAdmin_Returns403AndStaysPending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, _) = await SeedPendingAsync();
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task Approve_Anonymous_Returns401()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, _) = await SeedPendingAsync();
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reject_WithReason_RejectsAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, client) = await SeedPendingAsync();

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/reject", new { version = 1, reason = "Wrong unit" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
        question.RejectionReason.Should().Be("Wrong unit");
        (await ContentTestData.ReadAuditAsync(factory, "Question.Reject", questionId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Reject_BlankReason_Returns422QuestionRejectionReasonRequired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, client) = await SeedPendingAsync();

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/reject", new { version = 1, reason = "  " }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_REJECTION_REASON_REQUIRED");
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task Reject_RetiredQuestion_Returns400QuestionRetired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var questionId = await QuestionTestData.SeedRetiredQuestionAsync(factory, tree.LessonId, cancellationToken);
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.PostAsJsonAsync($"{Route}/questions/{questionId}/reject", new { version = 1, reason = "Wrong unit" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_RETIRED");
    }

    [Fact]
    public async Task PutQuestion_AsAssignedTeacher_Returns403AndContentUnchanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (questionId, _, client) = await SeedPendingAsync();

        using var response = await client.PutAsJsonAsync($"/api/questions/{questionId}", McqRequest("<p>Changed</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).Stem.Should().Be("<p>2 + 2 = ?</p>");
    }

    [Fact]
    public async Task ResubmitQuestion_AsAssignedTeacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var (teacher, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);
        var questionId = await QuestionTestData.SeedRejectedQuestionAsync(factory, tree.LessonId, teacher, "Wrong unit", cancellationToken);

        using var response = await client.PutAsJsonAsync($"/api/questions/{questionId}/resubmit", McqRequest("<p>Changed</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).Stem.Should().Be("<p>2 + 2 = ?</p>");
    }

    private async Task<(Guid QuestionId, Guid TeacherId, HttpClient Client)> SeedPendingAsync()
    {
        var tree = await SeedSubjectTreeAsync(factory, "Physics").ConfigureAwait(false);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var (teacher, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId).ConfigureAwait(false);
        return (questionId, teacher.Id, client);
    }
}
