using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class RuntimeSettingOverridePersistenceTests(ApiFactory factory)
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_DuplicateKey_ThrowsModifiedConcurrently()
    {
        var key = $"test.{Guid.NewGuid():N}";
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        first.RuntimeSettingOverrides.Add(RuntimeSettingOverride.Create(key, "1", AdminId));
        second.RuntimeSettingOverrides.Add(RuntimeSettingOverride.Create(key, "2", AdminId));
        await first.SaveChangesAsync(CancellationToken);

        var act = () => second.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RuntimeSettingModifiedConcurrently);
    }

    [Fact]
    public async Task SaveChanges_StaleVersion_ThrowsModifiedConcurrently()
    {
        var key = $"test.{Guid.NewGuid():N}";
        using (var seedScope = factory.Services.CreateScope())
        {
            var seed = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seed.RuntimeSettingOverrides.Add(RuntimeSettingOverride.Create(key, "1", AdminId));
            await seed.SaveChangesAsync(CancellationToken);
        }

        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstRow = await first.RuntimeSettingOverrides.SingleAsync(x => x.Key == key, CancellationToken);
        var secondRow = await second.RuntimeSettingOverrides.SingleAsync(x => x.Key == key, CancellationToken);
        firstRow.Override("2", AdminId);
        await first.SaveChangesAsync(CancellationToken);
        secondRow.Override("3", AdminId);

        var act = () => second.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RuntimeSettingModifiedConcurrently);
    }

    [Fact]
    public async Task Value_IsStoredAsJsonb()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataType = await context.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = {"RuntimeSettingOverrides"} AND column_name = {"Value"}")
            .SingleAsync(CancellationToken);

        dataType.Should().Be("jsonb");
    }
}
