using Elmanhg.Domain.Mastery;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Mastery;

public static class MasteryTestData
{
    public const string Route = "/api/mastery";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<Guid> PracticeAsync(HttpClient client, Guid lessonId, IReadOnlyDictionary<Guid, string> optionByQuestion)
    {
        var started = await SessionTestData.StartQuizAsync(client, lessonId).ConfigureAwait(false);
        var sessionId = started.GetProperty("id").GetGuid();
        var questionIds = started.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();
        foreach (var questionId in questionIds.Where(optionByQuestion.ContainsKey))
        {
            using var answer = await SessionTestData.AnswerAsync(client, sessionId, questionId, optionByQuestion[questionId]).ConfigureAwait(false);
            answer.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var finish = await SessionTestData.FinishAsync(client, sessionId).ConfigureAwait(false);
        finish.StatusCode.Should().Be(HttpStatusCode.OK);
        return sessionId;
    }

    public static async Task<JsonElement> GetOverviewAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{Route}/overview", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    public static JsonElement SubjectCard(JsonElement overview, Guid subjectId) => overview.GetProperty("subjects").EnumerateArray().Single(x => x.GetProperty("subjectId").GetGuid() == subjectId);

    public static async Task<List<QuestionMastery>> ReadMasteriesAsync(ApiFactory factory, Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.QuestionMasteries.AsNoTracking().Where(x => x.StudentId == studentId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task RetireAsync(ApiFactory factory, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var question = await context.Questions.SingleAsync(x => x.Id == questionId, CancellationToken).ConfigureAwait(false);
        question.Retire(Guid.NewGuid());
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task UnpublishAsync(ApiFactory factory, Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        lesson.Unpublish(Guid.NewGuid());
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }
}
