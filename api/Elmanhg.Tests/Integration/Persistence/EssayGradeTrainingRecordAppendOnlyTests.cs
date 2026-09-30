using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.EssayGrading;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class EssayGradeTrainingRecordAppendOnlyTests(ApiFactory factory)
{
    private const string Table = AppDbContext.EssayGradeTrainingRecordsTable;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Update_Row_RejectedByDatabase()
    {
        var recordId = await SeedRecordIdAsync();
        var recordedAt = await ReadRecordedAtAsync(recordId);

        var act = () => ExecuteAsync($"UPDATE \"{Table}\" SET \"RecordedAt\" = now() WHERE \"Id\" = @id", recordId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await ReadRecordedAtAsync(recordId)).Should().Be(recordedAt);
    }

    [Fact]
    public async Task Delete_Row_RejectedByDatabase()
    {
        var recordId = await SeedRecordIdAsync();

        var act = () => ExecuteAsync($"DELETE FROM \"{Table}\" WHERE \"Id\" = @id", recordId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await ReadRecordedAtAsync(recordId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Truncate_Table_RejectedByDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken);
        await using var truncate = connection.CreateCommand();
        truncate.Transaction = transaction;
        truncate.CommandText = $"TRUNCATE \"{Table}\"";

        var act = () => truncate.ExecuteNonQueryAsync(CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        await transaction.RollbackAsync(CancellationToken);
    }

    private async Task<Guid> SeedRecordIdAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var (gradeId, _, _) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id).ConfigureAwait(false);
        await EssayGradingTestData.GradeAsync(factory, gradeId).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().EssayGradeTrainingRecords.AsNoTracking().Where(x => x.EssayGradeId == gradeId).Select(x => x.Id).SingleAsync(CancellationToken).ConfigureAwait(false);
    }

    private Task<int> ExecuteAsync(string sql, Guid recordId) => RunAsync(sql, recordId, command => command.ExecuteNonQueryAsync(CancellationToken));

    private Task<object?> ReadRecordedAtAsync(Guid recordId) => RunAsync($"SELECT \"RecordedAt\" FROM \"{Table}\" WHERE \"Id\" = @id", recordId, command => command.ExecuteScalarAsync(CancellationToken));

    private async Task<T> RunAsync<T>(string sql, Guid recordId, Func<DbCommand, Task<T>> run)
    {
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(CancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter("id", recordId));
        return await run(command).ConfigureAwait(false);
    }
}
