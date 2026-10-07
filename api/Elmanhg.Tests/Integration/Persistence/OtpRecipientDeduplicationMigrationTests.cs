using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Infrastructure.Migrations;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class OtpRecipientDeduplicationMigrationTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Newest = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeduplicateRecipientsSql_DuplicateRecipients_KeepsTheNewestLiveRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (phone, otherPhone) = (AuthTestClient.NewPhoneNumber(), AuthTestClient.NewPhoneNumber());
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(transaction, $"DROP INDEX \"{AppDbContext.OtpRecipientIndex}\"");
        await InsertAsync(transaction, phone, Newest.AddHours(-2), isDeleted: false);
        await InsertAsync(transaction, phone, Newest, isDeleted: false);
        await InsertAsync(transaction, phone, Newest.AddHours(-1), isDeleted: false);
        await InsertAsync(transaction, phone, Newest.AddHours(1), isDeleted: true);
        await InsertAsync(transaction, otherPhone, Newest.AddHours(-3), isDeleted: false);

        await ExecuteAsync(transaction, AddOtpVersionAndRecipientIndex.DeduplicateRecipientsSql);

        (await ReadRowsAsync(transaction, phone)).Should().BeEquivalentTo([(Newest, false), (Newest.AddHours(1), true)]);
        (await ReadRowsAsync(transaction, otherPhone)).Should().Equal((Newest.AddHours(-3), false));
        await transaction.RollbackAsync(cancellationToken);
    }

    private static Task InsertAsync(DbTransaction transaction, string phone, DateTimeOffset createdAt, bool isDeleted) => ExecuteAsync(transaction, $"""
        INSERT INTO "Otps" ("Id", "VerificationId", "PhoneNumber", "RecipientType", "CodeHash", "VerificationAttempts", "MaxVerificationAttempts", "ReissueCount", "MaxReissueCount", "ReissueCooldownSeconds", "ReissueBlockCooldownInHours", "NextAllowedReissueAt", "ReissueWindowStartedAt", "IsVerified", "IsUsed", "CreatedAt", "ExpiresAt", "IsDeleted")
        VALUES ('{Guid.NewGuid()}', '{Guid.NewGuid()}', '{phone}', 'Phone', 'code-hash', 0, 3, 0, 5, 60, 24, '{createdAt:O}', '{createdAt:O}', false, false, '{createdAt:O}', '{createdAt.AddMinutes(5):O}', {(isDeleted ? "true" : "false")})
        """);

    private static async Task ExecuteAsync(DbTransaction transaction, string sql)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<(DateTimeOffset CreatedAt, bool IsDeleted)>> ReadRowsAsync(DbTransaction transaction, string phone)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT \"CreatedAt\", \"IsDeleted\" FROM \"Otps\" WHERE \"PhoneNumber\" = '{phone}'";
        List<(DateTimeOffset, bool)> rows = [];
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetFieldValue<DateTimeOffset>(0), reader.GetBoolean(1)));
        }

        return rows;
    }
}
