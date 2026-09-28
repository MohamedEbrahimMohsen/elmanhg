using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.QuestionValidation.ValidationTestData;

namespace Elmanhg.Tests.Integration.QuestionValidation;

public sealed class BulkApproveEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task StartSession_Teacher_ReturnsSessionAndExpiry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.PostAsync($"{Route}/review-sessions", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("reviewSessionId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(470));
    }

    [Fact]
    public async Task StartSession_AsAdmin_Returns403()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PostAsync($"{Route}/review-sessions", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecordOpening_AssignedQuestion_MarksItemOpenedInQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tree, client, questionIds) = await SeedQueueAsync(2);
        var sessionId = await StartSessionAsync(client);

        using var response = await client.PostAsync($"{Route}/review-sessions/{sessionId}/openings/{questionIds[0]}", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var queue = await client.GetAsync($"{Route}?reviewSessionId={sessionId}&lessonId={tree.LessonId}", cancellationToken);
        var items = await ReadItemsAsync(queue);
        items.Single(x => x.GetProperty("id").GetGuid() == questionIds[0]).GetProperty("openedInSession").GetBoolean().Should().BeTrue();
        items.Single(x => x.GetProperty("id").GetGuid() == questionIds[1]).GetProperty("openedInSession").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RecordOpening_OtherTeachersSession_Returns404ReviewSessionNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tree, owner, questionIds) = await SeedQueueAsync(1);
        var sessionId = await StartSessionAsync(owner);
        var (_, intruder) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await intruder.PostAsync($"{Route}/review-sessions/{sessionId}/openings/{questionIds[0]}", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("REVIEW_SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task RecordOpening_OtherSubject_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, _, questionIds) = await SeedQueueAsync(1);
        var other = await SeedSubjectTreeAsync(factory, "Math");
        var (_, client) = await SeedAssignedTeacherAsync(factory, other.SubjectId);
        var sessionId = await StartSessionAsync(client);

        using var response = await client.PostAsync($"{Route}/review-sessions/{sessionId}/openings/{questionIds[0]}", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task BulkApprove_AllOpened_ApprovesAllAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, client, questionIds) = await SeedQueueAsync(2);
        var sessionId = await OpenAllAsync(client, questionIds);

        using var response = await client.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = sessionId, questionIds }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("approvedCount").GetInt32().Should().Be(2);
        (await QuestionTestData.ReadQuestionAsync(factory, questionIds[0], cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        (await QuestionTestData.ReadQuestionAsync(factory, questionIds[1], cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        (await ContentTestData.ReadAuditAsync(factory, "Question.BulkApprove", sessionId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task BulkApprove_OneNotOpened_Returns400AndApprovesNone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, client, questionIds) = await SeedQueueAsync(2);
        var sessionId = await OpenAllAsync(client, [questionIds[0]]);

        using var response = await client.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = sessionId, questionIds }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_OPENED_IN_SESSION");
        (await QuestionTestData.ReadQuestionAsync(factory, questionIds[0], cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        (await QuestionTestData.ReadQuestionAsync(factory, questionIds[1], cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task BulkApprove_OpenedBeforeContentEdit_Returns400QuestionNotOpenedInSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, client, questionIds) = await SeedQueueAsync(1);
        var sessionId = await OpenAllAsync(client, questionIds);
        using var admin = await AdminClientAsync(factory);
        using var edit = await admin.PutAsJsonAsync($"/api/questions/{questionIds[0]}", McqRequest("<p>3 + 3 = ?</p>"), cancellationToken);
        edit.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await client.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = sessionId, questionIds }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_OPENED_IN_SESSION");
        (await QuestionTestData.ReadQuestionAsync(factory, questionIds[0], cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task BulkApprove_OtherTeachersSession_Returns404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (tree, owner, questionIds) = await SeedQueueAsync(1);
        var sessionId = await OpenAllAsync(owner, questionIds);
        var (_, intruder) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await intruder.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = sessionId, questionIds }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("REVIEW_SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task BulkApprove_EmptyList_Returns422QuestionIdsRequired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, client, _) = await SeedQueueAsync(1);
        var sessionId = await StartSessionAsync(client);

        using var response = await client.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = sessionId, questionIds = Array.Empty<Guid>() }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_IDS_REQUIRED");
    }

    [Fact]
    public async Task BulkApprove_AsAdmin_Returns403()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = Guid.NewGuid(), questionIds = new[] { Guid.NewGuid() } }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BulkApprove_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync($"{Route}/bulk-approve", new { reviewSessionId = Guid.NewGuid(), questionIds = new[] { Guid.NewGuid() } }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<((Guid SubjectId, Guid UnitId, Guid LessonId) Tree, HttpClient Client, List<Guid> QuestionIds)> SeedQueueAsync(int count)
    {
        var tree = await SeedSubjectTreeAsync(factory, "Physics").ConfigureAwait(false);
        List<Guid> questionIds = [];
        for (var index = 0; index < count; index++)
        {
            questionIds.Add(await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, TestContext.Current.CancellationToken).ConfigureAwait(false));
        }

        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId).ConfigureAwait(false);
        return (tree, client, questionIds);
    }

    private static async Task<Guid> OpenAllAsync(HttpClient client, List<Guid> questionIds)
    {
        var sessionId = await StartSessionAsync(client).ConfigureAwait(false);
        foreach (var questionId in questionIds)
        {
            using var response = await client.PostAsync($"{Route}/review-sessions/{sessionId}/openings/{questionId}", null, TestContext.Current.CancellationToken).ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return sessionId;
    }
}
