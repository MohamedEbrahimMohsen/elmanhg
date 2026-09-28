using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Exams;

public static class ExamTestData
{
    public const string Route = "/api/exams";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(Guid SubjectId, Guid UnitId, Guid LessonId, List<Guid> QuestionIds)> SeedExamUnitAsync(ApiFactory factory, int mcqCount, int? timeLimitMinutes = 30, int passMark = 50)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        List<Guid> questionIds = [];
        for (var index = 0; index < mcqCount; index++)
        {
            questionIds.Add(await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false));
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.SingleAsync(x => x.Id == unitId, CancellationToken).ConfigureAwait(false);
        context.ExamBlueprints.Add(ExamBlueprint.CreateForUnit(unit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, mcqCount)], null, timeLimitMinutes, passMark), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (subjectId, unitId, lessonId, questionIds);
    }

    public static async Task<JsonElement> StartAsync(HttpClient client, Guid unitId)
    {
        using var response = await client.PostAsync($"{Route}/units/{unitId}", null, CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    public static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid sessionId, Guid questionId, string optionId)
    {
        return client.PutAsJsonAsync($"{Route}/{sessionId}/answers/{questionId}", new { answer = new { optionId } }, CancellationToken);
    }

    public static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid sessionId) => client.PostAsync($"{Route}/{sessionId}/submit", null, CancellationToken);

    public static List<Guid> ItemQuestionIds(JsonElement session) => session.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();

    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }

    public static async Task RetireQuestionAsync(ApiFactory factory, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var question = await context.Questions.SingleAsync(x => x.Id == questionId, CancellationToken).ConfigureAwait(false);
        question.Retire(Guid.NewGuid());
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task ExpireAsync(ApiFactory factory, Guid sessionId, TimeSpan ago)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.ExecuteSqlAsync($"UPDATE \"Sessions\" SET \"Deadline\" = {DateTimeOffset.UtcNow - ago} WHERE \"Id\" = {sessionId}", CancellationToken).ConfigureAwait(false);
    }
}
