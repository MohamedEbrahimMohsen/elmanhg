using Core.Auditing.Entities;
using Core.Auditing.Repositories;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AuditLogAppendOnlyTests(ApiFactory factory)
{
    private const string Diff = """[{"entityType":"TeacherSubject","change":"Created","properties":{"subjectId":{"before":null,"after":"s-1"}}}]""";

    [Fact]
    public async Task AppendAsync_Entry_PersistsRowWithJsonbDiff()
    {
        var entry = await AppendAsync();

        var persisted = await ReadAsync(entry.Id);

        persisted.Should().NotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(persisted!.Diff!), JsonNode.Parse(Diff)).Should().BeTrue();
        persisted.Outcome.Should().Be(entry.Outcome);
        persisted.Action.Should().Be(entry.Action);
    }

    [Fact]
    public async Task Update_AuditLogRow_RejectedByDatabase()
    {
        var entry = await AppendAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => context.Database.ExecuteSqlAsync($"UPDATE \"AuditLogs\" SET \"Action\" = {"Tampered"} WHERE \"Id\" = {entry.Id}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await ReadAsync(entry.Id))!.Action.Should().Be(entry.Action);
    }

    [Fact]
    public async Task Delete_AuditLogRow_RejectedByDatabase()
    {
        var entry = await AppendAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => context.Database.ExecuteSqlAsync($"DELETE FROM \"AuditLogs\" WHERE \"Id\" = {entry.Id}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await ReadAsync(entry.Id)).Should().NotBeNull();
    }

    private async Task<AuditLog> AppendAsync()
    {
        var entry = new AuditLogBuilder().WithDiff(Diff).Build();
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAuditLogRepository>().AppendAsync(entry, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return entry;
    }

    private async Task<AuditLog?> ReadAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
