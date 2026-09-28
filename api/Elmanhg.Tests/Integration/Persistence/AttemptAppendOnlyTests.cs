using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AttemptAppendOnlyTests(ApiFactory factory)
{
    [Fact]
    public async Task Update_AttemptRow_RejectedByDatabase()
    {
        var (sessionId, attemptId) = await SeedAttemptAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => context.Database.ExecuteSqlAsync($"UPDATE \"Attempts\" SET \"Score\" = {0m}, \"NormalisedScore\" = {0m} WHERE \"Id\" = {attemptId}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Single().Score.Should().Be(1m);
    }

    [Fact]
    public async Task Delete_AttemptRow_RejectedByDatabase()
    {
        var (sessionId, attemptId) = await SeedAttemptAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => context.Database.ExecuteSqlAsync($"DELETE FROM \"Attempts\" WHERE \"Id\" = {attemptId}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle(x => x.Id == attemptId);
    }

    private async Task<(Guid SessionId, Guid AttemptId)> SeedAttemptAsync()
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        var (_, client) = await SessionTestData.SignedInStudentAsync(factory).ConfigureAwait(false);
        var sessionId = (await SessionTestData.StartQuizAsync(client, lessonId).ConfigureAwait(false)).GetProperty("id").GetGuid();
        using var response = await SessionTestData.AnswerAsync(client, sessionId, questionIds[0], "b").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (sessionId, (await SessionTestData.ReadAttemptsAsync(factory, sessionId).ConfigureAwait(false)).Single().Id);
    }
}
