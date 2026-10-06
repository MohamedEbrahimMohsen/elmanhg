using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Hosting;

public static class MigrationCommand<TContext> where TContext : DbContext
{
    public const string ConfigurationKey = "MigrateAndExit";

    public static bool IsRequested(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TContext>().Database;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationCommand<TContext>));
        var pending = (await database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        logger.LogInformation("Applying {Count} pending migrations: {Migrations}", pending.Count, pending);
        await database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Database is up to date.");
    }
}
