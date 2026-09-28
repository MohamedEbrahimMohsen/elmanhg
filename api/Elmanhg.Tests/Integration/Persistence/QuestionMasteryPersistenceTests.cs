using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Mastery;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Infrastructure.Migrations;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class QuestionMasteryPersistenceTests(ApiFactory factory)
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrate_QuestionMasteries_HasUniqueStudentQuestionIndex()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var definition = await context.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = {AppDbContext.QuestionMasteryPerStudentIndex}")
            .SingleAsync(TestContext.Current.CancellationToken);

        definition.Should().Contain("UNIQUE").And.Contain("\"StudentId\", \"QuestionId\"");
    }

    [Fact]
    public async Task Save_DuplicateStudentQuestion_ThrowsSessionModifiedConcurrently()
    {
        var (studentId, questionId) = await SeedAsync();
        await AddAsync(QuestionMastery.Start(studentId, questionId, new MasteryAttempt(Guid.NewGuid(), 1m, T0)));

        var act = () => AddAsync(QuestionMastery.Start(studentId, questionId, new MasteryAttempt(Guid.NewGuid(), 1m, T0.AddMinutes(1))));

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionModifiedConcurrently);
    }

    [Fact]
    public async Task Save_ConcurrentRecordOnSameRow_ThrowsSessionModifiedConcurrently()
    {
        var (studentId, questionId) = await SeedAsync();
        var row = QuestionMastery.Start(studentId, questionId, new MasteryAttempt(Guid.NewGuid(), 1m, T0));
        await AddAsync(row);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var (firstContext, first) = await LoadAsync(firstScope, row.Id);
        var (secondContext, second) = await LoadAsync(secondScope, row.Id);
        first.Record(new MasteryAttempt(Guid.NewGuid(), 1m, T0.AddMinutes(1)), 0.8m);
        second.Record(new MasteryAttempt(Guid.NewGuid(), 0m, T0.AddMinutes(2)), 0.8m);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionModifiedConcurrently);
    }

    [Fact]
    public async Task Backfill_ExistingAttempts_RebuildsMasteryExcludingTestMode()
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 2);
        var (q1, q2) = (questionIds[0], questionIds[1]);
        var (student, client) = await SessionTestData.SignedInStudentAsync(factory);
        var firstSession = await MasteryTestData.PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b", [q2] = "a" });
        var secondSession = await MasteryTestData.PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b" });
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var adminClient = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);
        await MasteryTestData.PracticeAsync(adminClient, lessonId, new Dictionary<Guid, string> { [q1] = "b" });
        var firstAttempts = await SessionTestData.ReadAttemptsAsync(factory, firstSession);
        var secondAttempt = (await SessionTestData.ReadAttemptsAsync(factory, secondSession)).Single();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.ExecuteSqlAsync($"DELETE FROM \"QuestionMasteries\" WHERE \"StudentId\" = {student.Id}", TestContext.Current.CancellationToken);

            await context.Database.ExecuteSqlRawAsync(AddQuestionMastery.BackfillSql, TestContext.Current.CancellationToken);
        }

        var rows = await MasteryTestData.ReadMasteriesAsync(factory, student.Id);
        var q1Row = rows.Single(x => x.QuestionId == q1);
        (q1Row.IsMastered, q1Row.LatestAttemptId, q1Row.PreviousAttemptId).Should().Be((true, secondAttempt.Id, firstAttempts.Single(x => x.QuestionId == q1).Id));
        var q2Row = rows.Single(x => x.QuestionId == q2);
        (q2Row.IsMastered, q2Row.LatestNormalisedScore, q2Row.PreviousAttemptId).Should().Be((false, 0m, (Guid?)null));
        (await MasteryTestData.ReadMasteriesAsync(factory, admin.Id)).Should().BeEmpty();
    }

    private async Task<(Guid StudentId, Guid QuestionId)> SeedAsync()
    {
        var (_, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (student.Id, questionIds[0]);
    }

    private async Task AddAsync(QuestionMastery row)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.QuestionMasteries.Add(row);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<(AppDbContext Context, QuestionMastery Row)> LoadAsync(IServiceScope scope, Guid id)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.QuestionMasteries.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (context, row);
    }
}
