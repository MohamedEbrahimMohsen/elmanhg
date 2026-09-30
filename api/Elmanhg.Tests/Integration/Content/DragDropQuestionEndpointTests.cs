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
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.Content;

public sealed class DragDropQuestionEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";
    private const string MessyBody = $$"""{"image":{"key":"{{DragDropImageKey}}","width":800,"height":600,"alt":"  Plant cell "},"zones":[{"id":"z1","x":10,"y":10,"width":20,"height":15,"capacity":2,"extra":1},{"id":"z2","x":50,"y":40,"width":30,"height":20.5,"capacity":2}],"items":[{"id":"i1","text":" Nucleus "},{"id":"i2","text":"Vacuole"},{"id":"i3","text":"Wall"},{"id":"i4","text":"Membrane"},{"id":"i5","text":"Engine"}],"extra":1}""";
    private const string MessySpec = """{"zones":[{"zoneId":"z2","itemIds":["i4","i3"],"ordered":true},{"zoneId":"z1","itemIds":["i2","i1"]}]}""";

    [Fact]
    public async Task Post_AdminDragDrop_StoresCanonicalDiagramAndKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, DragDropRequest(lessonId, MessyBody, MessySpec), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.Type.Should().Be(QuestionType.DragDrop);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(1);
        JsonNode.DeepEquals(JsonNode.Parse(question.Body), JsonNode.Parse(ForLesson(DragDropBodyJson, lessonId))).Should().BeTrue(question.Body);
        JsonNode.DeepEquals(JsonNode.Parse(question.GradingSpec), JsonNode.Parse(DragDropSpecJson)).Should().BeTrue(question.GradingSpec);
        question.Revisions.Should().ContainSingle();
    }

    [Fact]
    public async Task Get_AdminDragDrop_ResolvesImageUrlFromStoredKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var id = await CreateAsync(admin, lessonId);

        var detail = await admin.GetFromJsonAsync<JsonElement>($"{Route}/{id}", cancellationToken);

        var image = detail.GetProperty("body").GetProperty("image");
        image.GetProperty("key").GetString().Should().Be(ForLesson(DragDropImageKey, lessonId));
        image.GetProperty("url").GetString().Should().Be($"/api/media/{ForLesson(DragDropImageKey, lessonId)}");
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.Body.Should().NotContain("url");
    }

    [Fact]
    public async Task Post_OverlappingZones_Returns422QuestionDiagramZonesOverlap()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var body = DragDropBodyJson.Replace("""{"id":"z2","x":50,"y":40""", """{"id":"z2","x":25,"y":20""", StringComparison.Ordinal);

        using var response = await admin.PostAsJsonAsync(Route, DragDropRequest(lessonId, body, DragDropSpecJson), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_DIAGRAM_ZONES_OVERLAP");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_ExternalImageUrl_Returns422QuestionDiagramImageInvalid()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var body = DragDropBodyJson.Replace(DragDropImageKey, "https://evil.example/cell.png", StringComparison.Ordinal);

        using var response = await admin.PostAsJsonAsync(Route, DragDropRequest(lessonId, body, DragDropSpecJson), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_DIAGRAM_IMAGE_INVALID");
    }

    [Fact]
    public async Task Post_DiagramKeyFromAnotherLesson_Returns422QuestionDiagramImageInvalid()
    {
        var (_, lessonId) = await SeedLessonAsync();
        var (_, otherLessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, DragDropRequest(lessonId, ForLesson(DragDropBodyJson, otherLessonId), DragDropSpecJson), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_DIAGRAM_IMAGE_INVALID");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_DragDropAsTeacher_Returns403()
    {
        var (subjectId, lessonId) = await SeedLessonAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsJsonAsync(Route, DragDropRequest(lessonId, DragDropBodyJson, DragDropSpecJson), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_DragDropKeyChange_BumpsVersionAndSnapshotsKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var id = await CreateAsync(admin, lessonId);
        var spec = DragDropSpecJson.Replace("\"ordered\":true", "\"ordered\":false", StringComparison.Ordinal);

        using var response = await admin.PutAsJsonAsync($"{Route}/{id}", DragDropRequest(lessonId, DragDropBodyJson, spec), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.Version.Should().Be(2);
        question.Revisions.Should().HaveCount(2);
        var zone = JsonNode.Parse(question.Revisions.Single(x => x.Version == 2).Snapshot)!["gradingSpec"]!["zones"]![1]!;
        zone["ordered"]!.GetValue<bool>().Should().BeFalse();
        zone["itemIds"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Equal("i3", "i4");
    }

    [Fact]
    public async Task PostGradeDraft_DragDrop_ReturnsPerItemGrade()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        var answer = Json("""{"placements":[{"zoneId":"z1","itemIds":["i1","i2"]},{"zoneId":"z2","itemIds":["i4"]}]}""");
        var request = new { type = "DragDrop", stem = "<p>Label the plant cell.</p>", body = Json(DragDropBodyJson), gradingSpec = Json(DragDropSpecJson), difficulty = "Medium", maxScore = 4, lessonId, answer };

        using var response = await admin.PostAsJsonAsync($"{Route}/grade-draft", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("score").GetDecimal().Should().Be(3m);
        body.GetProperty("outcome").GetString().Should().Be("Partial");
        body.GetProperty("feedback").GetString().Should().NotBeNullOrEmpty();
    }

    private static object DragDropRequest(Guid lessonId, string body, string gradingSpec)
    {
        return new { lessonId, type = "DragDrop", stem = "<p>Label the plant cell.</p>", body = Json(ForLesson(body, lessonId)), gradingSpec = Json(gradingSpec), explanation = "<p>Parts of a cell.</p>", difficulty = "Medium", tags = Array.Empty<string>(), maxScore = 4 };
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, Guid lessonId)
    {
        using var created = await admin.PostAsJsonAsync(Route, DragDropRequest(lessonId, DragDropBodyJson, DragDropSpecJson), TestContext.Current.CancellationToken).ConfigureAwait(false);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Biology", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Cells", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Plant cells", 1, [], TestContext.Current.CancellationToken).ConfigureAwait(false);
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
