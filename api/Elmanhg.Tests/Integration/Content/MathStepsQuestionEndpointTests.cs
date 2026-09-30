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

public sealed class MathStepsQuestionEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";
    private const string MessySpec = """{"acceptedAnswers":["  x = 2 ","2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"}""";
    private const string CanonicalSpec = """{"acceptedAnswers":["x = 2","2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"}""";
    private const string FinalOnlyFeedback = "صُحّحت الإجابة النهائية فقط، وتُصحَّح الخطوات لاحقًا.";

    [Fact]
    public async Task Post_MathStepsQuestion_StoresCanonicalSpec()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, MathRequest(lessonId, MessySpec), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        var question = await QuestionTestData.ReadQuestionAsync(factory, id, cancellationToken);
        question.Type.Should().Be(QuestionType.MathSteps);
        JsonNode.DeepEquals(JsonNode.Parse(question.Body), JsonNode.Parse("{}")).Should().BeTrue();
        JsonNode.DeepEquals(JsonNode.Parse(question.GradingSpec), JsonNode.Parse(CanonicalSpec)).Should().BeTrue();
    }

    [Fact]
    public async Task Post_ToleranceWithFactoredForm_Returns422ToleranceFormConflict()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, MathRequest(lessonId, """{"acceptedAnswers":["(x+1)^2"],"form":"factored","tolerance":0.1,"toleranceMode":"absolute"}"""), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_MATH_TOLERANCE_FORM_CONFLICT");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_GradeDraftMathSteps_ReturnsCorrectWithFeedback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();
        admin.DefaultRequestHeaders.Add("Accept-Language", "ar");

        using var response = await admin.PostAsJsonAsync($"{Route}/grade-draft", GradeDraftRequest(), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        (body.GetProperty("outcome").GetString(), body.GetProperty("score").GetDecimal(), body.GetProperty("feedback").GetString()).Should().Be(("Correct", 2m, FinalOnlyFeedback));
    }

    [Fact]
    public async Task Post_GradeDraftMathSteps_AsStudent_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync($"{Route}/grade-draft", GradeDraftRequest(), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static object GradeDraftRequest()
    {
        var answer = Json("""{"steps":["2x = 4"],"finalAnswer":"x=2"}""");
        return new { type = "MathSteps", stem = "<p>Solve 2x + 3 = 7.</p>", body = Json("{}"), gradingSpec = Json(MathStepsSpecJson), difficulty = "Medium", maxScore = 2, answer };
    }

    private static object MathRequest(Guid lessonId, string gradingSpec)
    {
        return new { lessonId, type = "MathSteps", stem = "<p>Solve 2x + 3 = 7.</p>", body = Json("""{"x":1}"""), gradingSpec = Json(gradingSpec), explanation = "<p>Subtract 3, divide by 2.</p>", difficulty = "Medium", tags = Array.Empty<string>(), maxScore = 2 };
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Mathematics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Algebra", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Linear equations", 1, ["Solve a linear equation"], TestContext.Current.CancellationToken).ConfigureAwait(false);
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

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
