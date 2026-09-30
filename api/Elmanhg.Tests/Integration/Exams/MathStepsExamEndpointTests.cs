using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class MathStepsExamEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Put_MathStepsAnswer_StoresCanonicalAnswer()
    {
        var (client, sessionId, mathId) = await StartMathExamAsync();

        using var response = await SaveMathAsync(client, sessionId, mathId, new { steps = new[] { " 2x = 4 " }, finalAnswer = " x=2 " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await SessionTestData.ReadSessionAsync(factory, sessionId);
        var saved = session.Items.Single(x => x.QuestionId == mathId).SavedAnswer;
        JsonNode.DeepEquals(JsonNode.Parse(saved!), JsonNode.Parse("""{"steps":["2x = 4"],"finalAnswer":"x=2"}""")).Should().BeTrue();
    }

    [Fact]
    public async Task Submit_SavedMathStepsAnswer_GradesFinalAnswer()
    {
        var (client, sessionId, mathId) = await StartMathExamAsync();
        using var saved = await SaveMathAsync(client, sessionId, mathId, new { steps = new[] { "2x = 4" }, finalAnswer = "x=2" });

        using var response = await SubmitAsync(client, sessionId);

        (saved.StatusCode, response.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        var attempt = (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle(x => x.QuestionId == mathId).Which;
        (attempt.Outcome, attempt.Score).Should().Be((GradeOutcome.Correct, 2m));
    }

    private async Task<(HttpClient Client, Guid SessionId, Guid MathId)> StartMathExamAsync()
    {
        var (unitId, mathId) = await SeedMathUnitAsync();
        var (_, client) = await SessionTestData.SignedInStudentAsync(factory);
        var session = await StartAsync(client, unitId);
        ItemQuestionIds(session).Should().Contain(mathId);
        return (client, session.GetProperty("id").GetGuid(), mathId);
    }

    private async Task<(Guid UnitId, Guid MathId)> SeedMathUnitAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Mathematics", 1, cancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Algebra", 1, cancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Linear equations", 1, LessonState.Published, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        var mathId = await QuestionTestData.SeedMathStepsQuestionAsync(factory, lessonId, cancellationToken);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.SingleAsync(x => x.Id == unitId, cancellationToken);
        var shape = new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1), new ExamTypeCount(QuestionType.MathSteps, 1)], null, 30, 50);
        context.ExamBlueprints.Add(ExamBlueprint.CreateForUnit(unit, shape, ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));
        await context.SaveChangesAsync(cancellationToken);
        return (unitId, mathId);
    }

    private static Task<HttpResponseMessage> SaveMathAsync(HttpClient client, Guid sessionId, Guid questionId, object answer)
    {
        return client.PutAsJsonAsync($"{Route}/{sessionId}/answers/{questionId}", new { answer }, TestContext.Current.CancellationToken);
    }
}
