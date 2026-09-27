using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Core.Logging;

public static class DependencyInjection
{
    // Entry for APIs (cleanest usage)
    public static IServiceCollection AddCoreLogging(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration
            .GetSection(LoggingOptions.SectionName)
            .Get<LoggingOptions>() ?? new LoggingOptions(); // ✅ fallback

        return services.AddCoreLogging(options, environment.EnvironmentName);
    }

    // Core method (reusable internally)
    private static IServiceCollection AddCoreLogging( this IServiceCollection services, LoggingOptions options, string environmentName)
    {
        var loggerConfig = CreateBaseConfiguration(environmentName);

        ConfigureConsole(loggerConfig, options.Console, environmentName);
        ConfigureFile(loggerConfig, options.File);
        ConfigureAzureTable(loggerConfig, options.AzureTable);
        ConfigureApplicationInsights(loggerConfig, options.AppInsights);
        ConfigureSeq(loggerConfig, options.Seq);

        var logger = loggerConfig.CreateLogger();
        Log.Logger = logger;

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger);
        });

        return services;
    }

    // Base config
    private static LoggerConfiguration CreateBaseConfiguration(string environmentName)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithMachineName()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", environmentName);
    }

    // Console
    private static void ConfigureConsole(LoggerConfiguration config, ConsoleLoggingOptions options, string environmentName)
    {
        if (!options.Enabled) 
            return;

        if (options.Environments?.Length > 0 &&
            !options.Environments.Contains(environmentName))
            return;

        config.WriteTo.Async(a => a.Console(
            restrictedToMinimumLevel: options.MinimumLevel
        ));
    }

    // File
    private static void ConfigureFile(LoggerConfiguration config, FileLoggingOptions options)
    {
        if (!options.Enabled) 
            return;

        var folder = options.BasePath ?? "Logs";
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "log-.log");

        config.WriteTo.Async(a => a.File(
            path: path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: options.RetentionInDays,
            restrictedToMinimumLevel: options.MinimumLevel,
            outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] [{TraceId}] {Message:lj}{NewLine}{Exception}"
        ));
    }

    // Azure Table Storage
    private static void ConfigureAzureTable(LoggerConfiguration config, AzureTableLoggingOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
            return;

        var period = options.PeriodSeconds is { } s ? TimeSpan.FromSeconds(s) : (TimeSpan?)null;
        string[] azureRequestLogPropertyColumns = ["TraceId", "ErrorCode", "DebugId", "Method", "Path", "QueryString", "Host", "ClientIp", "ForwardedFor", "Country", "City", "State", "Region", "Timezone", "Latitude", "Longitude", "UserAgent", "StatusCode", "MachineName", "DurationMs", "Environment", "ServiceName", "Version", "Cluster"];
        string[] azureLogsPropertyColumns = ["TraceId", "Environment"];
        
        config.WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("IsRequestLog"))
            .WriteTo.AzureTableStorage(
                connectionString: options.ConnectionString,
                restrictedToMinimumLevel: options.MinimumLevel,
                storageTableName: options.RequestLogsTableName,
                period: period,
                batchPostingLimit: options.BatchPostingLimit,
                propertyColumns: azureRequestLogPropertyColumns
            )
        );

        config.WriteTo.Logger(lc => lc
            .Filter.ByExcluding(e => e.Properties.ContainsKey("IsRequestLog"))
            .WriteTo.AzureTableStorage(
                connectionString: options.ConnectionString,
                restrictedToMinimumLevel: options.MinimumLevel,
                storageTableName: options.LogsTableName,
                period: period,
                batchPostingLimit: options.BatchPostingLimit,
                propertyColumns: azureLogsPropertyColumns
            )
        );
    }

    // Log Analytics (Azure Monitor)
    private static void ConfigureApplicationInsights(LoggerConfiguration config, AppInsightsLoggingOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
            return;

        var telemetryConfig = TelemetryConfiguration.CreateDefault();
        telemetryConfig.ConnectionString = options.ConnectionString;

        config.WriteTo.ApplicationInsights(
            telemetryConfig,
            TelemetryConverter.Traces,
            restrictedToMinimumLevel: options.MinimumLevel
        );
    }

    // Seq
    private static void ConfigureSeq(LoggerConfiguration config, SeqLoggingOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ServerUrl))
            return;

        config.WriteTo.Async(a => a.Seq(
            serverUrl: options.ServerUrl,
            apiKey: options.ApiKey,
            restrictedToMinimumLevel: options.MinimumLevel
        ));
    }
}