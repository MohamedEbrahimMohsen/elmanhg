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

public sealed class EssayValidationEndpointTests(ApiFactory factory)
{
    private const string ModelAnswer = "<p>Inertia is resistance to change in motion.</p>";

    [Fact]
    public async Task GetQuestion_Essay_ReturnsRubricAndModelAnswers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        using var admin = await AdminClientAsync(factory);
        var questionId = await CreateEssayAsync(admin, tree.LessonId);
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.GetAsync($"{Route}/questions/{questionId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("type").GetString().Should().Be("Essay");
        var spec = body.GetProperty("gradingSpec");
        spec.GetProperty("criteria")[0].GetProperty("levels").GetArrayLength().Should().Be(3);
        spec.GetProperty("modelAnswers")[0].GetString().Should().Be(ModelAnswer);
    }

    [Fact]
    public async Task GetQueue_TypeEssay_ListsPendingEssaysOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        using var admin = await AdminClientAsync(factory);
        var essayId = await CreateEssayAsync(admin, tree.LessonId);
        await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, cancellationToken);
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.GetAsync($"{Route}?type=Essay", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadItemsAsync(response)).Select(x => x.GetProperty("id").GetGuid()).Should().Equal(essayId);
    }

    [Fact]
    public async Task Approve_EssayInPublishedLesson_ApprovedAndServable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, tree.UnitId, "Inertia", 2, LessonState.Published, cancellationToken);
        using var admin = await AdminClientAsync(factory);
        var questionId = await CreateEssayAsync(admin, lessonId);
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

    private static async Task<Guid> CreateEssayAsync(HttpClient admin, Guid lessonId)
    {
        var request = new { lessonId, type = "Essay", stem = "<p>Explain inertia.</p>", body = Json("""{"maxWords":200}"""), gradingSpec = Json(EssaySpecJson), explanation = "<p>Newton 1.</p>", difficulty = "Medium", tags = Array.Empty<string>(), maxScore = 5 };
        using var response = await admin.PostAsJsonAsync("/api/questions", request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("id").GetGuid();
    }
}
