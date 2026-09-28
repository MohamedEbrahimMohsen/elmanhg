using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
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

[Collection(ServableCountCollection.Name)]
public sealed class ServableQuestionCountEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions/servable-count";

    [Fact]
    public async Task Get_Anonymous_Returns200WithCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Published);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Get_AfterLessonPublished_CountsItsApprovedQuestions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Draft);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var admin = await AdminClientAsync();
        var baseline = await ReadCountAsync(admin);

        using var response = await admin.PostAsync($"/api/lessons/{lessonId}/publish", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(admin)).Should().Be(baseline + 1);
    }

    [Fact]
    public async Task Get_AfterQuestionRetired_ExcludesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Published);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();
        var baseline = await ReadCountAsync(admin);

        using var response = await admin.PostAsync($"/api/questions/{questionId}/retire", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(admin)).Should().Be(baseline - 1);
    }

    [Fact]
    public async Task Get_AfterApprovedContentEdited_ExcludesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Published);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();
        var baseline = await ReadCountAsync(admin);
        var request = new { lessonId, type = "Mcq", stem = "<p>3 + 3 = ?</p>", body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), explanation = "<p>Add the numbers.</p>", difficulty = "Medium", maxScore = 1 };

        using var response = await admin.PutAsJsonAsync($"/api/questions/{questionId}", request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(admin)).Should().Be(baseline - 1);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task Get_AfterLessonArchived_ExcludesItsQuestions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Published);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();
        var baseline = await ReadCountAsync(admin);

        using var response = await admin.PostAsync($"/api/lessons/{lessonId}/archive", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(admin)).Should().Be(baseline - 1);
    }

    [Fact]
    public async Task Get_AfterLessonUnpublished_ExcludesItsQuestions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Published);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();
        var baseline = await ReadCountAsync(admin);

        using var response = await admin.PostAsync($"/api/lessons/{lessonId}/unpublish", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(admin)).Should().Be(baseline - 1);
    }

    [Fact]
    public async Task Get_AfterTeacherApproves_CountsIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, lessonId) = await SeedAssignedLessonAsync(LessonState.Published);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var teacher = await TeacherClientAsync(subjectId);
        var baseline = await ReadCountAsync(teacher);

        using var response = await teacher.PostAsJsonAsync($"/api/validation-queue/questions/{questionId}/approve", new { version = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(teacher)).Should().Be(baseline + 1);
    }

    [Fact]
    public async Task Get_AfterTeacherRejects_RefreshesCachedCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var draftLessonId = await SeedLessonAsync(LessonState.Draft);
        await QuestionTestData.SeedQuestionAsync(factory, draftLessonId, approved: true, cancellationToken);
        var (subjectId, lessonId) = await SeedAssignedLessonAsync(LessonState.Published);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var teacher = await TeacherClientAsync(subjectId);
        var baseline = await ReadCountAsync(teacher);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync($"UPDATE \"Lessons\" SET \"State\" = 'Published' WHERE \"Id\" = {draftLessonId}", cancellationToken);
        }

        using var response = await teacher.PostAsJsonAsync($"/api/validation-queue/questions/{questionId}/reject", new { version = 1, reason = "Wrong unit" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCountAsync(teacher)).Should().Be(baseline + 1);
    }

    private async Task<Guid> SeedLessonAsync(LessonState state)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, state, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedAssignedLessonAsync(LessonState state)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, state, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId);
    }

    private async Task<HttpClient> TeacherClientAsync(Guid subjectId)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadCountAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(Route, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("count").GetInt32();
    }
}
