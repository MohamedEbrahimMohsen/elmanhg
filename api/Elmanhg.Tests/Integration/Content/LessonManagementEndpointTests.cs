using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Content;

public sealed class LessonManagementEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/lessons";

    [Fact]
    public async Task PutPosition_MoveLastToFirst_RenumbersLessonsAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, unitId) = await SeedUnitAsync();
        var first = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "A", 1, LessonState.Draft, cancellationToken);
        var second = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "B", 2, LessonState.Published, cancellationToken);
        var third = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "C", 3, LessonState.Draft, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{third}/position", new { position = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ContentTestData.ReadLessonAsync(factory, first, cancellationToken)).Order.Should().Be(2);
        (await ContentTestData.ReadLessonAsync(factory, second, cancellationToken)).Order.Should().Be(3);
        (await ContentTestData.ReadLessonAsync(factory, third, cancellationToken)).Order.Should().Be(1);
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.Reorder", third, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PutPosition_PositionZero_Returns422LessonPositionInvalid()
    {
        var lessonId = await SeedLessonAsync(LessonState.Draft);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{lessonId}/position", new { position = 0 }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("LESSON_POSITION_INVALID");
    }

    [Fact]
    public async Task PutPosition_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}/position", new { position = 1 }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task PutPosition_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Draft, cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route}/{lessonId}/position", new { position = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_DraftLesson_SoftDeletesWithObjectivesAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law", "Apply F = ma"], cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lesson = await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken);
        lesson.IsDeleted.Should().BeTrue();
        lesson.Objectives.Should().HaveCount(2).And.OnlyContain(x => x.IsDeleted);
        using var list = await admin.GetAsync($"{Route}?unitId={unitId}", cancellationToken);
        (await list.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().NotContain(lessonId);
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.Delete", lessonId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Delete_PublishedLesson_Returns400LessonIsPublished()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Published);
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("LESSON_IS_PUBLISHED");
        (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_LessonWithQuestions_Returns400LessonHasQuestions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(LessonState.Draft);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("LESSON_HAS_QUESTIONS");
        (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Draft, cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.DeleteAsync($"{Route}/{lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).IsDeleted.Should().BeFalse();
    }

    private async Task<Guid> SeedLessonAsync(LessonState state)
    {
        var (_, unitId) = await SeedUnitAsync().ConfigureAwait(false);
        return await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, state, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<(Guid SubjectId, Guid UnitId)> SeedUnitAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, unitId);
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
