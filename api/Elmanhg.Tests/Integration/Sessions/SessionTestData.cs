using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Sessions;

public static class SessionTestData
{
    public const string Route = "/api/sessions";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(Guid LessonId, List<Guid> QuestionIds)> SeedServableLessonAsync(ApiFactory factory, int approvedCount)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        List<Guid> questionIds = [];
        for (var index = 0; index < approvedCount; index++)
        {
            questionIds.Add(await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false));
        }

        return (lessonId, questionIds);
    }

    public static async Task<(User Student, HttpClient Client)> SignedInStudentAsync(ApiFactory factory)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        return (student, await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken).ConfigureAwait(false));
    }

    public static async Task<JsonElement> StartQuizAsync(HttpClient client, Guid lessonId, int? questionCount = null)
    {
        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId, questionCount }, CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    public static Task<HttpResponseMessage> AnswerAsync(HttpClient client, Guid sessionId, Guid questionId, string optionId, int? timeTakenMilliseconds = 1000)
    {
        return client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId, answer = new { optionId }, timeTakenMilliseconds }, CancellationToken);
    }

    public static Task<HttpResponseMessage> FinishAsync(HttpClient client, Guid sessionId) => client.PostAsync($"{Route}/{sessionId}/finish", null, CancellationToken);

    public static async Task EditQuestionContentAsync(ApiFactory factory, Guid questionId, QuestionContent content)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var question = await context.Questions.Include(x => x.Revisions).SingleAsync(x => x.Id == questionId, CancellationToken).ConfigureAwait(false);
        var lesson = await context.Lessons.Include(x => x.Objectives).SingleAsync(x => x.Id == question.LessonId, CancellationToken).ConfigureAwait(false);
        question.Update(QuestionType.Mcq, content, new QuestionMetadata(question.Difficulty, question.ObjectiveId, question.Tags), lesson, Guid.NewGuid());
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task<Session> ReadSessionAsync(ApiFactory factory, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Sessions.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery().AsNoTracking().SingleAsync(x => x.Id == sessionId, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<Attempt>> ReadAttemptsAsync(ApiFactory factory, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Attempts.AsNoTracking().Where(x => x.SessionId == sessionId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
