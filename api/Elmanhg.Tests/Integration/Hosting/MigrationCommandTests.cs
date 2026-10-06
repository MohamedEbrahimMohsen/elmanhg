using Core.Hosting;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Hosting;

public sealed class MigrationCommandTests(ApiFactory factory)
{
    [Fact]
    public void IsRequested_CommandLineFlagTrue_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder().AddCommandLine(["--MigrateAndExit=true"]).Build();

        var requested = MigrationCommand<AppDbContext>.IsRequested(configuration);

        requested.Should().BeTrue();
    }

    [Fact]
    public void IsRequested_FlagMissing_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var requested = MigrationCommand<AppDbContext>.IsRequested(configuration);

        requested.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_MigratedDatabase_LeavesNoPendingMigrations()
    {
        await MigrationCommand<AppDbContext>.RunAsync(factory.Services, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        var pending = await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var applied = await database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        pending.Should().BeEmpty();
        applied.Should().HaveSameCount(database.GetMigrations());
    }
}
