using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.ExamBlueprints;

public sealed class ExamBlueprintsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/exam-blueprints";
    private static readonly object NullEntryBody = new { typeCounts = new object?[] { null }, passMark = 50 };

    [Fact]
    public async Task Get_Admin_ReturnsServableCountsPerUnitAndSubject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        var unitA = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 2, cancellationToken);
        var unitB = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 1, cancellationToken);
        var lessonA = await ExamBlueprintTestData.SeedServableMcqAsync(factory, unitA, 2, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonA, approved: false, cancellationToken);
        var draftLesson = await ContentTestData.SeedLessonInStateAsync(factory, unitB, "Draft lesson", 1, LessonState.Draft, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, draftLesson, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/subjects/{subjectId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("defaultBlueprint").ValueKind.Should().Be(JsonValueKind.Null);
        McqCount(body.GetProperty("servable")).Should().Be(2);
        var units = body.GetProperty("units").EnumerateArray().ToList();
        units.Select(x => x.GetProperty("unitId").GetGuid()).Should().Equal(unitB, unitA);
        McqCount(units[1].GetProperty("servable")).Should().Be(2);
        McqCount(units[0].GetProperty("servable")).Should().Be(0);
    }

    [Fact]
    public async Task Get_UnknownSubject_Returns404SubjectNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/subjects/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.GetAsync($"{Route}/subjects/{subjectId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{Route}/subjects/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutSubject_Admin_CreatesDefaultAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, _) = await SeedServableSubjectAsync(2);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/subjects/{subjectId}", Body(2), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("unitId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("questionCount").GetInt32().Should().Be(2);
        var id = body.GetProperty("id").GetGuid();
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken)).Should().ContainSingle(x => x.Id == id);
        (await ContentTestData.ReadAuditAsync(factory, "ExamBlueprint.SaveSubjectDefault", id, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PutSubject_Twice_UpdatesSameBlueprint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, _) = await SeedServableSubjectAsync(2);
        using var admin = await AdminClientAsync();
        var firstId = await PutAsync(admin, $"{Route}/subjects/{subjectId}", Body(2));

        using var response = await admin.PutAsJsonAsync($"{Route}/subjects/{subjectId}", Body(1, passMark: 75), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("id").GetGuid().Should().Be(firstId);
        body.GetProperty("passMark").GetInt32().Should().Be(75);
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken)).Should().ContainSingle();
    }

    [Fact]
    public async Task PutSubject_Shortfall_Returns400AndSavesNothing()
    {
        var (subjectId, _) = await SeedServableSubjectAsync(1);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/subjects/{subjectId}", Body(2), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_BLUEPRINT_SHORTFALL");
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task PutSubject_AllZero_Returns422Empty()
    {
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/subjects/{subjectId}", Body(0), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("EXAM_BLUEPRINT_EMPTY");
    }

    [Fact]
    public async Task PutSubject_NullTypeCountEntry_Returns422TypeCountRequired()
    {
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/subjects/{subjectId}", NullEntryBody, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED");
    }

    [Fact]
    public async Task PutSubject_Teacher_Returns403()
    {
        var (subjectId, _) = await SeedServableSubjectAsync(1);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route}/subjects/{subjectId}", Body(1), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task PutUnit_Admin_CreatesUnitBlueprint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedServableSubjectAsync(1);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/units/{unitId}", Body(1), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("unitId").GetGuid().Should().Be(unitId);
        body.GetProperty("subjectId").GetGuid().Should().Be(subjectId);
        var row = (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken)).Single();
        (row.UnitId, row.SubjectId).Should().Be(((Guid?)unitId, subjectId));
        var overview = await admin.GetFromJsonAsync<JsonElement>($"{Route}/subjects/{subjectId}", cancellationToken);
        overview.GetProperty("defaultBlueprint").ValueKind.Should().Be(JsonValueKind.Null);
        overview.GetProperty("units")[0].GetProperty("blueprint").GetProperty("id").GetGuid().Should().Be(row.Id);
    }

    [Fact]
    public async Task PutUnit_QuestionsInOtherUnit_Returns400Shortfall()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, _) = await SeedServableSubjectAsync(2);
        var unitB = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/units/{unitB}", Body(1), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_BLUEPRINT_SHORTFALL");
    }

    [Fact]
    public async Task PutUnit_UnknownUnit_Returns404UnitNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/units/{Guid.NewGuid()}", Body(1), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task PutUnit_NullTypeCountEntry_Returns422TypeCountRequired()
    {
        var (_, unitId) = await SeedServableSubjectAsync(1);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/units/{unitId}", NullEntryBody, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED");
    }

    [Fact]
    public async Task PutUnit_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PutAsJsonAsync($"{Route}/units/{Guid.NewGuid()}", Body(1), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_UnitBlueprint_SoftDeletesAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedServableSubjectAsync(1);
        using var admin = await AdminClientAsync();
        var id = await PutAsync(admin, $"{Route}/units/{unitId}", Body(1));

        using var response = await admin.DeleteAsync($"{Route}/{id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken)).Single().IsDeleted.Should().BeTrue();
        (await ContentTestData.ReadAuditAsync(factory, "ExamBlueprint.Delete", id, cancellationToken)).Outcome.Should().Be("Success");
        var overview = await admin.GetFromJsonAsync<JsonElement>($"{Route}/subjects/{subjectId}", cancellationToken);
        overview.GetProperty("units")[0].GetProperty("blueprint").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Delete_SubjectDefault_Returns400DefaultNotDeletable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, _) = await SeedServableSubjectAsync(1);
        using var admin = await AdminClientAsync();
        var id = await PutAsync(admin, $"{Route}/subjects/{subjectId}", Body(1));

        using var response = await admin.DeleteAsync($"{Route}/{id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE");
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken)).Single().IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("EXAM_BLUEPRINT_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedServableSubjectAsync(1);
        using var admin = await AdminClientAsync();
        var id = await PutAsync(admin, $"{Route}/units/{unitId}", Body(1));
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.DeleteAsync($"{Route}/{id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken)).Single().IsDeleted.Should().BeFalse();
    }

    private static object Body(int mcq, int passMark = 50) => new { typeCounts = new[] { new { type = "Mcq", count = mcq } }, timeLimitMinutes = 45, passMark };

    private static int McqCount(JsonElement servable) => servable.EnumerateArray().Single(x => x.GetProperty("type").GetString() == "Mcq").GetProperty("count").GetInt32();

    private Task<Guid> SeedSubjectAsync() => ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken);

    private async Task<(Guid SubjectId, Guid UnitId)> SeedServableSubjectAsync(int mcq)
    {
        var subjectId = await SeedSubjectAsync().ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await ExamBlueprintTestData.SeedServableMcqAsync(factory, unitId, mcq, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, unitId);
    }

    private static async Task<Guid> PutAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PutAsJsonAsync(url, body, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("id").GetGuid();
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
