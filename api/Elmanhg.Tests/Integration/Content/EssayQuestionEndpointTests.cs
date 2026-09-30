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

public sealed class EssayQuestionEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";
    private const string MessySpec = """{"criteria":[{"id":"c1","title":"  Definition ","description":"  ","points":2,"levels":[{"points":2,"description":"Complete"},{"points":0,"description":"Missing"},{"points":1,"description":" Partial "}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]}""";

    [Fact]
    public async Task Post_AdminEssay_StoresCanonicalBodyAndRubric()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, EssayRequest(lessonId, MessySpec), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.Type.Should().Be(QuestionType.Essay);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(1);
        JsonNode.DeepEquals(JsonNode.Parse(question.GradingSpec), JsonNode.Parse(EssaySpecJson)).Should().BeTrue();
        JsonNode.DeepEquals(JsonNode.Parse(question.Body), JsonNode.Parse("""{"maxWords":200}""")).Should().BeTrue();
        question.Revisions.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_EssayBrokenLevelScale_Returns422QuestionRubricLevelPointsInvalid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, EssayRequest(lessonId, Spec(points: 2, top: 1)), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_RUBRIC_LEVEL_POINTS_INVALID");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_EssayAsTeacher_Returns403()
    {
        var (subjectId, lessonId) = await SeedLessonAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsJsonAsync(Route, EssayRequest(lessonId, EssaySpecJson), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_EssayRubricChange_BumpsVersionAndSnapshotsRubric()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var created = await admin.PostAsJsonAsync(Route, EssayRequest(lessonId, EssaySpecJson), cancellationToken);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        using var response = await admin.PutAsJsonAsync($"{Route}/{id}", EssayRequest(lessonId, Spec(points: 3, top: 3)), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.Version.Should().Be(2);
        question.Revisions.Should().HaveCount(2);
        var snapshot = JsonNode.Parse(question.Revisions.Single(x => x.Version == 2).Snapshot)!;
        snapshot["gradingSpec"]!["criteria"]![0]!["points"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public async Task PostGradeDraft_Essay_ReturnsAiGradeWithCriteria()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync($"{Route}/grade-draft", GradeDraftRequest(lessonId, Json("""{"text":"Inertia is resistance to change."}""")), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        (body.GetProperty("score").GetDecimal(), body.GetProperty("outcome").GetString()).Should().Be((5m, "Correct"));
        var essay = body.GetProperty("essay");
        (essay.GetProperty("criteria")[0].GetProperty("points").GetInt32(), essay.GetProperty("model").GetString()).Should().Be((2, "fake"));
    }

    [Fact]
    public async Task PostGradeDraft_EssayWithoutText_Returns422QuestionAnswerInvalid()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync($"{Route}/grade-draft", GradeDraftRequest(lessonId, Json("{}")), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_ANSWER_INVALID");
    }

    [Fact]
    public async Task Post_EssayEmptyModelAnswer_Returns422QuestionModelAnswerRequired()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, EssayRequest(lessonId, ModelAnswerSpec("<p></p>")), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_MODEL_ANSWER_REQUIRED");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_EssayModelAnswerEmptyAfterSanitising_Returns422QuestionModelAnswerRequired()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, EssayRequest(lessonId, ModelAnswerSpec("<p><script>x</script></p>")), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_MODEL_ANSWER_REQUIRED");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    private static object GradeDraftRequest(Guid lessonId, JsonElement answer)
    {
        return new { type = "Essay", stem = "<p>Explain inertia.</p>", body = Json("""{"maxWords":200}"""), gradingSpec = Json(EssaySpecJson), difficulty = "Medium", maxScore = 5, lessonId, answer };
    }

    private static string ModelAnswerSpec(string modelAnswer)
    {
        return $$"""{"criteria":[{"id":"c1","title":"Definition","points":2,"levels":[{"points":0,"description":"Missing"},{"points":2,"description":"Complete"}]}],"modelAnswers":["{{modelAnswer}}"]}""";
    }

    private static object EssayRequest(Guid lessonId, string gradingSpec)
    {
        return new { lessonId, type = "Essay", stem = "<p>Explain inertia.</p>", body = Json("""{"maxWords":200}"""), gradingSpec = Json(gradingSpec), explanation = "<p>Newton 1.</p>", difficulty = "Medium", tags = Array.Empty<string>(), maxScore = 5 };
    }

    private static string Spec(int points, int top)
    {
        return $$"""{"criteria":[{"id":"c1","title":"Definition","points":{{points}},"levels":[{"points":0,"description":"Missing"},{"points":{{top}},"description":"Complete"}]}],"modelAnswers":["<p>Inertia.</p>"]}""";
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law"], TestContext.Current.CancellationToken).ConfigureAwait(false);
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
