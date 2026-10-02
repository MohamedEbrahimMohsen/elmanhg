using Elmanhg.Domain.Avatar;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Avatar;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AvatarConversationErasureTests(ApiFactory factory)
{
    private const string Setting = "elmanhg.erase_avatar_conversation";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Erase_Conversation_DeletesItsMessagesAndTrainingRecordsOnly()
    {
        var erased = await SeedAsync();
        var kept = await SeedAsync();
        var counted = await SeedAsync(exchanges: 2);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAvatarConversationRepository>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await repository.ExecuteInTransactionAsync(token => repository.EraseMessagesAsync(erased.Id, token), CancellationToken);
        var returned = await context.Database.SqlQuery<int>($"SELECT erase_avatar_conversation({counted.Id}) AS \"Value\"").ToListAsync(CancellationToken);

        (await AvatarTestData.ReadConversationAsync(factory, erased.Id))!.Messages.Should().BeEmpty();
        (await AvatarTestData.ReadTrainingRecordsAsync(factory, erased.Id)).Should().BeEmpty();
        (await AvatarTestData.ReadConversationAsync(factory, kept.Id))!.Messages.Select(x => x.Id).Should().BeEquivalentTo(kept.Messages.Select(x => x.Id));
        (await AvatarTestData.ReadTrainingRecordsAsync(factory, kept.Id)).Should().ContainSingle();
        returned.Should().Equal(4);
        (await AvatarTestData.ReadConversationAsync(factory, counted.Id))!.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_MessageWithFlagForAnotherConversation_RejectedByDatabase()
    {
        var flagged = await SeedAsync();
        var other = await SeedAsync();
        var messageId = other.Messages[0].Id;

        var act = () => InTransactionAsync(async command =>
        {
            await ExecuteAsync(command, $"SELECT set_config('{Setting}', @id::text, true)", flagged.Id);
            await ExecuteAsync(command, "DELETE FROM \"AvatarMessages\" WHERE \"Id\" = @id", messageId);
        });

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await AvatarTestData.ReadConversationAsync(factory, other.Id))!.Messages.Should().Contain(x => x.Id == messageId);
    }

    [Fact]
    public async Task Update_MessageWithFlagForSameConversation_RejectedByDatabase()
    {
        var conversation = await SeedAsync();
        var messageId = conversation.Messages[0].Id;

        var act = () => InTransactionAsync(async command =>
        {
            await ExecuteAsync(command, $"SELECT set_config('{Setting}', @id::text, true)", conversation.Id);
            await ExecuteAsync(command, "UPDATE \"AvatarMessages\" SET \"Text\" = 'changed' WHERE \"Id\" = @id", messageId);
        });

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await AvatarTestData.ReadConversationAsync(factory, conversation.Id))!.Messages.Single(x => x.Id == messageId).Text.Should().Be("question");
    }

    [Fact]
    public async Task Delete_TrainingRecordWithoutFlag_RejectedByDatabase()
    {
        var conversation = await SeedAsync();

        var act = () => InTransactionAsync(command => ExecuteAsync(command, "DELETE FROM \"AvatarTrainingRecords\" WHERE \"ConversationId\" = @id", conversation.Id));

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await AvatarTestData.ReadTrainingRecordsAsync(factory, conversation.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Erase_Conversation_ClearsTheFlagBeforeReturning()
    {
        var conversation = await SeedAsync();
        object? erased = null;
        object? setting = null;

        await InTransactionAsync(async command =>
        {
            command.Parameters.Add(new NpgsqlParameter("id", conversation.Id));
            command.CommandText = "SELECT erase_avatar_conversation(@id)";
            erased = await command.ExecuteScalarAsync(CancellationToken);
            command.Parameters.Clear();
            command.CommandText = $"SELECT current_setting('{Setting}', true)";
            setting = await command.ExecuteScalarAsync(CancellationToken);
        });

        (erased, setting).Should().Be(((object?)2, (object?)string.Empty));
    }

    [Fact]
    public async Task Delete_MessageAfterFlagTransactionEnds_RejectedByDatabase()
    {
        var conversation = await SeedAsync();
        var messageId = conversation.Messages[0].Id;
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(CancellationToken);
        await using (var transaction = await connection.BeginTransactionAsync(CancellationToken))
        {
            await using var flag = Command(connection, transaction);
            await ExecuteAsync(flag, $"SELECT set_config('{Setting}', @id::text, true)", conversation.Id);
            await transaction.CommitAsync(CancellationToken);
        }

        await using var delete = Command(connection, null);
        var act = () => ExecuteAsync(delete, "DELETE FROM \"AvatarMessages\" WHERE \"Id\" = @id", messageId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await AvatarTestData.ReadConversationAsync(factory, conversation.Id))!.Messages.Should().Contain(x => x.Id == messageId);
    }

    [Fact]
    public async Task Truncate_AvatarMessages_RejectedByDatabase()
    {
        var act = () => InTransactionAsync(command =>
        {
            command.CommandText = "TRUNCATE \"AvatarMessages\"";
            return command.ExecuteNonQueryAsync(CancellationToken);
        });

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
    }

    private async Task<AvatarConversation> SeedAsync(int exchanges = 1)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var builder = new AvatarConversationBuilder().ForStudent(student.Id);
        for (var index = 0; index < exchanges; index++)
        {
            builder.WithExchange("question", "reply");
        }

        var conversation = builder.Build();
        await AvatarTestData.SeedConversationAsync(factory, conversation).ConfigureAwait(false);
        return conversation;
    }

    private async Task InTransactionAsync(Func<DbCommand, Task> run)
    {
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(CancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, transaction);
        await run(command).ConfigureAwait(false);
        await transaction.RollbackAsync(CancellationToken).ConfigureAwait(false);
    }

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        return command;
    }

    private static Task<int> ExecuteAsync(DbCommand command, string sql, Guid id)
    {
        command.CommandText = sql;
        command.Parameters.Clear();
        command.Parameters.Add(new NpgsqlParameter("id", id));
        return command.ExecuteNonQueryAsync(CancellationToken);
    }
}
