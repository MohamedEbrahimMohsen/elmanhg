using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Progress;

public static class ProgressTestData
{
    public const string Route = "/api/progress";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<JsonElement> GetJsonAsync(HttpClient client, string relativePath)
    {
        using var response = await client.GetAsync($"{Route}/{relativePath}", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    public static async Task<(Guid LessonId, Guid ObjectiveId)> SeedPublishedLessonWithObjectiveAsync(ApiFactory factory, Guid unitId, string name, int order, string objectiveText, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.SingleAsync(x => x.Id == unitId, cancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var lesson = Lesson.Create(unit, name, order, creator);
        lesson.Update(name, string.Empty, string.Empty, null, [new LessonObjectiveContent(null, objectiveText)], creator);
        lesson.Publish(creator);
        lesson.ClearDomainEvents();
        context.Lessons.Add(lesson);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (lesson.Id, lesson.Objectives[0].Id);
    }

    public static async Task<Guid> SeedObjectiveQuestionAsync(ApiFactory factory, Guid lessonId, Guid objectiveId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, cancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, cancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var question = Question.Create(lesson, unit, QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, objectiveId, []), creator);
        question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator), question.Version);
        context.Questions.Add(question);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return question.Id;
    }

    public static async Task<Guid> InsertUnitExamSessionAsync(ApiFactory factory, Guid studentId, Guid unitId, decimal? scorePercent, bool submitted, bool isTestMode, DateTimeOffset startedAt)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        var examScope = new UnitExamScope(unitId);
        var scopeJson = examScope.ToJson();
        var scopeKey = examScope.ToKey();
        DateTimeOffset? submittedAt = submitted ? startedAt.AddMinutes(30) : null;
        await context.Database.ExecuteSqlAsync($"INSERT INTO \"Sessions\" (\"Id\",\"CreatedBy\",\"CreationDate\",\"IsDeleted\",\"IsTestMode\",\"Kind\",\"LastActivityAt\",\"Scope\",\"ScopeKey\",\"ScorePercent\",\"StartedAt\",\"StudentId\",\"SubmittedAt\",\"UpdationDate\") VALUES ({id},{studentId},{startedAt},false,{isTestMode},'UnitExam',{startedAt},CAST({scopeJson} AS jsonb),{scopeKey},{scorePercent},{startedAt},{studentId},{submittedAt},{startedAt})", CancellationToken).ConfigureAwait(false);
        return id;
    }
}
