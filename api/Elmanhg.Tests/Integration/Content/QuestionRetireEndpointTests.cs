using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.Content;

public sealed class QuestionRetireEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";

    [Fact]
    public async Task Post_ApprovedQuestion_RetiresAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{questionId}/retire", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken);
        question.RetiredAt.Should().NotBeNull();
        (question.ValidationStatus, question.Version).Should().Be((QuestionValidationStatus.Approved, 1));
        (await ContentTestData.ReadAuditAsync(factory, "Question.Retire", questionId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Post_RetiredQuestion_Returns400QuestionAlreadyRetired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedRetiredQuestionAsync(factory, lessonId, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{questionId}/retire", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_ALREADY_RETIRED");
    }

    [Fact]
    public async Task Post_UnknownQuestion_Returns404QuestionNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{Guid.NewGuid()}/retire", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.PostAsync($"{Route}/{questionId}/retire", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).RetiredAt.Should().BeNull();
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.PostAsync($"{Route}/{questionId}/retire", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).RetiredAt.Should().BeNull();
    }

    [Fact]
    public async Task Put_RetiredQuestion_Returns400QuestionRetired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var questionId = await QuestionTestData.SeedRetiredQuestionAsync(factory, lessonId, cancellationToken);
        using var admin = await AdminClientAsync();
        var request = new { lessonId, type = "Mcq", stem = "<p>3 + 3 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), explanation = "<p>Add the numbers.</p>", difficulty = "Medium", maxScore = 1 };

        using var response = await admin.PutAsJsonAsync($"{Route}/{questionId}", request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_RETIRED");
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).Version.Should().Be(1);
    }

    [Fact]
    public async Task GetList_RetiredQuestion_IsListedWithRetiredAtAndNotServable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var retiredId = await QuestionTestData.SeedRetiredQuestionAsync(factory, lessonId, cancellationToken);
        var servableId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?lessonId={lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetGuid());
        items[retiredId].GetProperty("retiredAt").ValueKind.Should().Be(JsonValueKind.String);
        items[retiredId].GetProperty("isServable").GetBoolean().Should().BeFalse();
        items[servableId].GetProperty("isServable").GetBoolean().Should().BeTrue();
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
