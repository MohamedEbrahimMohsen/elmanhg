using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;
using static Elmanhg.Tests.Integration.QuestionValidation.ValidationTestData;

namespace Elmanhg.Tests.Integration.QuestionValidation;

public sealed class DragDropValidationEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GetQuestion_DragDrop_ReturnsDiagramAndKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Biology");
        using var admin = await AdminClientAsync(factory);
        var questionId = await CreateDragDropAsync(admin, tree.LessonId);
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.GetAsync($"{Route}/questions/{questionId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("type").GetString().Should().Be("DragDrop");
        body.GetProperty("body").GetProperty("zones").GetArrayLength().Should().Be(2);
        body.GetProperty("body").GetProperty("image").GetProperty("url").GetString().Should().Be($"/api/media/{ForLesson(DragDropImageKey, tree.LessonId)}");
        body.GetProperty("gradingSpec").GetProperty("zones")[1].GetProperty("ordered").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Approve_DragDropInPublishedLesson_BecomesServable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Biology");
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, tree.UnitId, "Plant cells", 2, LessonState.Published, cancellationToken);
        using var admin = await AdminClientAsync(factory);
        var questionId = await CreateDragDropAsync(admin, lessonId);
        var (_, teacher) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await teacher.PostAsJsonAsync($"{Route}/questions/{questionId}/approve", new { version = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lessons = await admin.GetFromJsonAsync<JsonElement>($"/api/lessons?unitId={tree.UnitId}", cancellationToken);
        var lesson = lessons.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == lessonId);
        lesson.GetProperty("questionCount").GetInt32().Should().Be(1);
        lesson.GetProperty("servableQuestionCount").GetInt32().Should().Be(1);
        var questions = await admin.GetFromJsonAsync<JsonElement>($"/api/questions?lessonId={lessonId}", cancellationToken);
        var item = questions.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("validationStatus").GetString().Should().Be("Approved");
        item.GetProperty("isServable").GetBoolean().Should().BeTrue();
    }

    private static async Task<Guid> CreateDragDropAsync(HttpClient admin, Guid lessonId)
    {
        var request = new { lessonId, type = "DragDrop", stem = "<p>Label the plant cell.</p>", body = Json(ForLesson(DragDropBodyJson, lessonId)), gradingSpec = Json(DragDropSpecJson), explanation = "<p>Parts of a cell.</p>", difficulty = "Medium", tags = Array.Empty<string>(), maxScore = 4 };
        using var response = await admin.PostAsJsonAsync("/api/questions", request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("id").GetGuid();
    }
}
