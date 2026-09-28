using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Content;

public sealed class LessonsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/lessons";

    [Fact]
    public async Task Post_Admin_CreatesDraftLessonAtEndAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, unitId) = await SeedUnitAsync();
        await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, [], cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, new { unitId, name = "Momentum" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        var lesson = await ContentTestData.ReadLessonAsync(factory, id, cancellationToken);
        lesson.Order.Should().Be(2);
        lesson.State.Should().Be(LessonState.Draft);
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.Create", id, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Post_UnknownUnit_Returns404UnitNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, new { unitId = Guid.NewGuid(), name = "Momentum" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task Post_EmptyName_Returns422LessonNameRequired()
    {
        var (_, unitId) = await SeedUnitAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, new { unitId, name = string.Empty }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("LESSON_NAME_REQUIRED");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var (subjectId, unitId) = await SeedUnitAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsJsonAsync(Route, new { unitId, name = "Momentum" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(Route, new { unitId = Guid.NewGuid(), name = "Momentum" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetList_Admin_ReturnsUnitLessonsInOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, unitId) = await SeedUnitAsync();
        await ContentTestData.SeedLessonAsync(factory, unitId, "Momentum", 2, [], cancellationToken);
        await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, [], cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?unitId={unitId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lessons = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().ToList();
        lessons.Select(x => x.GetProperty("name").GetString()).Should().Equal("Newton's laws", "Momentum");
        lessons.Should().OnlyContain(x => x.GetProperty("state").GetString() == "Draft");
    }

    [Fact]
    public async Task GetList_UnknownUnit_Returns404UnitNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?unitId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_Admin_ReturnsLessonWithOrderedObjectives()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law", "Apply F = ma"], cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("objectives").EnumerateArray().Select(x => x.GetProperty("text").GetString()).Should().Equal("State the first law", "Apply F = ma");
        body.GetProperty("videoUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetById_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_Student_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, [], cancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, cancellationToken);

        using var response = await client.GetAsync($"{Route}/{lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_Admin_SanitisesContentAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync([]);
        using var admin = await AdminClientAsync();
        var explanation = "<p>Force</p><script>alert(1)</script><span data-type=\"inline-math\" data-latex=\"F=ma\"></span>";

        using var response = await admin.PutAsJsonAsync($"{Route}/{lessonId}", new { name = "Newton's laws", explanation, summary = "<p>Summary</p>", videoUrl = "https://example.com/video", objectives = Array.Empty<object>() }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lesson = await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken);
        lesson.Explanation.Should().Contain("data-latex=\"F=ma\"").And.NotContain("<script");
        lesson.VideoUrl.Should().Be("https://example.com/video");
        var audit = await ContentTestData.ReadAuditAsync(factory, "Lesson.Update", lessonId, cancellationToken);
        audit.Outcome.Should().Be("Success");
        audit.Diff.Should().NotBeNull();
    }

    [Fact]
    public async Task Put_ObjectiveList_KeepsIdsAddsNewAndSoftDeletesRemoved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync(["A", "B"]);
        var seeded = (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).Objectives;
        var a = seeded.Single(x => x.Text == "A").Id;
        var b = seeded.Single(x => x.Text == "B").Id;
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{lessonId}", UpdateBody([new { id = (Guid?)b, text = "B2" }, new { id = (Guid?)null, text = "C" }]), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var objectives = (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).Objectives;
        objectives.Single(x => x.Id == a).IsDeleted.Should().BeTrue();
        var kept = objectives.Single(x => x.Id == b);
        (kept.Order, kept.Text).Should().Be((1, "B2"));
        var added = objectives.Single(x => x.Id != a && x.Id != b);
        (added.Order, added.Text, added.IsDeleted).Should().Be((2, "C", false));
    }

    [Fact]
    public async Task Put_UnknownObjectiveId_Returns400LessonObjectiveUnknown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync([]);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{lessonId}", UpdateBody([new { id = (Guid?)Guid.NewGuid(), text = "Unknown" }]) with { Name = "Momentum" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("LESSON_OBJECTIVE_UNKNOWN");
        (await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).Name.Should().Be("Newton's laws");
    }

    [Fact]
    public async Task Put_EmptyName_Returns422LessonNameRequired()
    {
        var lessonId = await SeedLessonAsync([]);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{lessonId}", UpdateBody([]) with { Name = string.Empty }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("LESSON_NAME_REQUIRED");
    }

    [Fact]
    public async Task Put_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", UpdateBody([]), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Put_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedUnitAsync();
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, [], cancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route}/{lessonId}", UpdateBody([]), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static UpdateLessonBody UpdateBody(object[] objectives) => new("Newton's laws", "<p>Force</p>", string.Empty, null, objectives);

    private async Task<(Guid SubjectId, Guid UnitId)> SeedUnitAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, unitId);
    }

    private async Task<Guid> SeedLessonAsync(IReadOnlyList<string> objectiveTexts)
    {
        var (_, unitId) = await SeedUnitAsync().ConfigureAwait(false);
        return await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, objectiveTexts, TestContext.Current.CancellationToken).ConfigureAwait(false);
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

    private sealed record UpdateLessonBody(string Name, string Explanation, string Summary, string? VideoUrl, object[] Objectives);
}
