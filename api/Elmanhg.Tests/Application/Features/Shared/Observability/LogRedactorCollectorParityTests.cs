using Core.Logging;
using Elmanhg.Application.Shared.Observability;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class LogRedactorCollectorParityTests
{
    private static readonly string CollectorConfigPath = Path.Combine("deploy", "observability", "otel-collector", "config.yaml");

    [Fact]
    public void CollectorConfig_PhoneStatements_RunTheLogRedactorPatternTwiceInEveryContext()
    {
        var config = ReadCollectorConfig();

        Occurrences(config, Escaped(LogRedactor.PhonePattern)).Should().Be(8);
        Occurrences(config, Escaped(LogRedactor.PhoneReplacementPattern)).Should().Be(8);
    }

    [Fact]
    public void CollectorConfig_EmailStatements_UseTheSharedEmailPattern()
    {
        var config = ReadCollectorConfig();

        Occurrences(config, Escaped(RedactionPatterns.EmailAddress.ToString())).Should().Be(4);
    }

    [Fact]
    public void CollectorConfig_PhoneStatements_HaveNoWordBoundary()
    {
        var config = ReadCollectorConfig();

        config.Should().NotContain(@"\\b");
    }

    private static string ReadCollectorConfig()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, CollectorConfigPath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"{CollectorConfigPath} was not found above the test output directory.");
    }

    private static string Escaped(string value) => value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("$", "$$", StringComparison.Ordinal);

    private static int Occurrences(string text, string value) => text.Split(value).Length - 1;
}
