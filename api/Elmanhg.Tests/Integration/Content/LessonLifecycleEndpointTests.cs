using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Content;

public sealed class LessonLifecycleEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/lessons";

    [Fact]
    public async Task PostPublish_DraftLesson_PublishesAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (lessonId, _) = await SeedLessonAsync(LessonState.Draft);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/publish", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lesson = await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken);
        lesson.State.Should().Be(LessonState.Published);
        lesson.PublishedAt.Should().NotBeNull();
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.Publish", lessonId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PostPublish_DraftLesson_DispatchesLessonPublished()
    {
        var (lessonId, unitId) = await SeedLessonAsync(LessonState.Draft);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/publish", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.LessonEvents.Events.Should().Contain(new LessonPublished(lessonId, unitId));
    }

    [Fact]
    public async Task PostPublish_PublishedLesson_Returns400LessonAlreadyPublished()
    {
        var (lessonId, _) = await SeedLessonAsync(LessonState.Published);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/publish", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("LESSON_ALREADY_PUBLISHED");
    }

    [Fact]
    public async Task PostPublish_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{Guid.NewGuid()}/publish", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task PostPublish_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Draft, cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsync($"{Route}/{lessonId}/publish", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).State.Should().Be(LessonState.Draft);
    }

    [Fact]
    public async Task PostPublish_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync($"{Route}/{Guid.NewGuid()}/publish", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostUnpublish_PublishedLesson_ReturnsToDraftAndDispatchesEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (lessonId, unitId) = await SeedLessonAsync(LessonState.Published);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/unpublish", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lesson = await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken);
        lesson.State.Should().Be(LessonState.Draft);
        lesson.PublishedAt.Should().NotBeNull();
        factory.LessonEvents.Events.Should().Contain(new LessonUnpublished(lessonId, unitId));
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.Unpublish", lessonId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PostUnpublish_DraftLesson_Returns400LessonAlreadyDraft()
    {
        var (lessonId, _) = await SeedLessonAsync(LessonState.Draft);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/unpublish", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("LESSON_ALREADY_DRAFT");
    }

    [Fact]
    public async Task PostUnpublish_Student_Returns403()
    {
        var (lessonId, _) = await SeedLessonAsync(LessonState.Published);
        using var client = await StudentClientAsync();

        using var response = await client.PostAsync($"{Route}/{lessonId}/unpublish", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostArchive_PublishedLesson_ArchivesAndDispatchesEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (lessonId, unitId) = await SeedLessonAsync(LessonState.Published);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/archive", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).State.Should().Be(LessonState.Archived);
        factory.LessonEvents.Events.Should().Contain(new LessonArchived(lessonId, unitId));
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.Archive", lessonId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PostArchive_DraftLesson_Returns400LessonNotPublished()
    {
        var (lessonId, _) = await SeedLessonAsync(LessonState.Draft);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{Route}/{lessonId}/archive", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_PUBLISHED");
    }

    [Fact]
    public async Task PostArchive_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsync($"{Route}/{lessonId}/archive", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid LessonId, Guid UnitId)> SeedLessonAsync(LessonState state)
    {
        var (_, unitId) = await SeedUnitAsync().ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, state, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (lessonId, unitId);
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

    private async Task<HttpClient> StudentClientAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, student, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
