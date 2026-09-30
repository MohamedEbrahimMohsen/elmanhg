using Elmanhg.Application.LoadTesting.SeedLoadTestData;
using MediatR;
using System.Text.RegularExpressions;

namespace Elmanhg.Api.Hosting;

public static partial class LoadTestSeedCommand
{
    public const string ConfigurationKey = "SeedLoadTestAndExit";
    public const string DefaultKey = "loadtest";
    public const int DefaultStudentCount = 60;
    public const int MaxStudentCount = 500;

    public static bool IsRequested(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);

    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LoadTestSeedCommand));
        if (environment.IsProduction())
        {
            logger.LogError("Load-test seed refuses to run in Production.");
            return 1;
        }

        var key = configuration["LoadTestSeed:Key"] ?? DefaultKey;
        var count = configuration.GetValue("LoadTestSeed:StudentCount", DefaultStudentCount);
        var password = configuration["LoadTestSeed:StudentPassword"];
        if (!KeyPattern().IsMatch(key) || count < 1 || count > MaxStudentCount || string.IsNullOrWhiteSpace(password))
        {
            logger.LogError("Load-test seed settings are invalid for key {Key}: the key must match ^[a-z0-9-]{{1,20}}$, the student count must be 1 to {MaxStudentCount}, and LoadTestSeed:StudentPassword is required.", key, MaxStudentCount);
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedLoadTestDataCommand(key, count, password), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Load-test data {Key}: subject {SubjectId}, {Lessons} lessons, {Questions} questions, {Students} new students", key, result.SubjectId, result.LessonsCreated, result.QuestionsCreated, result.StudentsCreated);
        return 0;
    }

    [GeneratedRegex("^[a-z0-9-]{1,20}$")]
    private static partial Regex KeyPattern();
}
