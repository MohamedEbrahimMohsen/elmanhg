using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TrainingData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class TrainingRecordAppendOnlyTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AppDbContext.AttemptTrainingRecordsTable)]
    [InlineData(AppDbContext.AvatarTrainingRecordsTable)]
    [InlineData(AppDbContext.TeacherThreadTrainingRecordsTable)]
    public async Task Update_TrainingRecordRow_RejectedByDatabase(string table)
    {
        var recordId = await TrainingDataTestData.SeedRecordIdAsync(factory, table);
        var recordedAt = await ReadRecordedAtAsync(table, recordId);

        var act = () => ExecuteAsync($"UPDATE \"{table}\" SET \"RecordedAt\" = now() WHERE \"Id\" = @id", recordId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await ReadRecordedAtAsync(table, recordId)).Should().Be(recordedAt);
    }

    [Theory]
    [InlineData(AppDbContext.AttemptTrainingRecordsTable)]
    [InlineData(AppDbContext.AvatarTrainingRecordsTable)]
    [InlineData(AppDbContext.TeacherThreadTrainingRecordsTable)]
    public async Task Delete_TrainingRecordRow_RejectedByDatabase(string table)
    {
        var recordId = await TrainingDataTestData.SeedRecordIdAsync(factory, table);

        var act = () => ExecuteAsync($"DELETE FROM \"{table}\" WHERE \"Id\" = @id", recordId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await ReadRecordedAtAsync(table, recordId)).Should().NotBeNull();
    }

    [Theory]
    [InlineData(AppDbContext.AttemptTrainingRecordsTable)]
    [InlineData(AppDbContext.AvatarTrainingRecordsTable)]
    [InlineData(AppDbContext.TeacherThreadTrainingRecordsTable)]
    public async Task Truncate_TrainingRecordTable_RejectedByDatabase(string table)
    {
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken);
        await using var truncate = connection.CreateCommand();
        truncate.Transaction = transaction;
        truncate.CommandText = $"TRUNCATE \"{table}\"";

        var act = () => truncate.ExecuteNonQueryAsync(CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        await transaction.RollbackAsync(CancellationToken);
    }

    private Task<int> ExecuteAsync(string sql, Guid recordId) => RunAsync(sql, recordId, command => command.ExecuteNonQueryAsync(CancellationToken));

    private Task<object?> ReadRecordedAtAsync(string table, Guid recordId) => RunAsync($"SELECT \"RecordedAt\" FROM \"{table}\" WHERE \"Id\" = @id", recordId, command => command.ExecuteScalarAsync(CancellationToken));

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
