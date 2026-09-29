using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Api.Hosting;

public static class MigrationCommand
{
    public const string ConfigurationKey = "MigrateAndExit";

    public static bool IsRequested(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationCommand));
        var pending = (await database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        logger.LogInformation("Applying {Count} pending migrations: {Migrations}", pending.Count, pending);
        await database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Database is up to date.");
    }
}
