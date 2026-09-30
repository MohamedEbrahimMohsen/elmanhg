using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AttemptQueryPlanTests(ApiFactory factory)
{
    [Fact]
    public async Task AttemptsByStudentSince_Plan_UsesStudentCreatedAtIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var disableSeqScan = connection.CreateCommand();
        disableSeqScan.Transaction = transaction;
        disableSeqScan.CommandText = "SET LOCAL enable_seqscan = off";
        await disableSeqScan.ExecuteNonQueryAsync(cancellationToken);
        await using var explain = connection.CreateCommand();
        explain.Transaction = transaction;
        explain.CommandText = $"EXPLAIN SELECT COUNT(*) FROM \"Attempts\" WHERE \"StudentId\" = '{Guid.NewGuid()}' AND \"CreatedAt\" >= '2026-09-01T00:00:00Z' AND \"IsDeleted\" = false";

        var plan = new StringBuilder();
        await using (var reader = await explain.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                plan.AppendLine(reader.GetString(0));
            }
        }

        plan.ToString().Should().Contain(AppDbContext.AttemptStudentCreatedAtIndex);
        await transaction.RollbackAsync(cancellationToken);
    }
}
