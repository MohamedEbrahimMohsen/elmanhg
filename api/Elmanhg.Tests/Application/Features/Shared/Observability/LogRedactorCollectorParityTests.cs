using Core.Logging;
using Elmanhg.Application.Shared.Observability;
using FluentAssertions;
using System.Globalization;

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

    [Theory]
    [InlineData("log", "replace_pattern(body, \"{0}\", \"{1}\") where IsString(body)")]
    [InlineData("log", "replace_all_patterns(attributes, \"value\", \"{0}\", \"{1}\")")]
    [InlineData("span", "replace_all_patterns(attributes, \"value\", \"{0}\", \"{1}\")")]
    [InlineData("spanevent", "replace_all_patterns(attributes, \"value\", \"{0}\", \"{1}\")")]
    public void CollectorConfig_PhoneStatements_RunTheLogRedactorPatternTwiceInContext(string context, string statementFormat)
    {
        var expected = string.Format(CultureInfo.InvariantCulture, statementFormat, Escaped(LogRedactor.PhonePattern), Escaped(LogRedactor.PhoneReplacementPattern));
        var prefix = statementFormat[..statementFormat.IndexOf('"', StringComparison.Ordinal)];

        ContextStatements(ReadCollectorConfig(), context)
            .Where(x => x.StartsWith(prefix, StringComparison.Ordinal) && x.Contains(LogRedactor.PhoneReplacement, StringComparison.Ordinal))
            .Should().Equal(expected, expected);
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

    private static List<string> ContextStatements(string config, string context)
    {
        var lines = config.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
        var contextLine = lines.Where(x => x.Trim() == $"- context: {context}").Should().ContainSingle().Subject;
        var contextIndent = Indent(contextLine);
        return lines.Skip(lines.IndexOf(contextLine) + 1)
            .TakeWhile(x => string.IsNullOrWhiteSpace(x) || Indent(x) > contextIndent)
            .Select(x => x.Trim())
            .Where(x => x.StartsWith("- ", StringComparison.Ordinal))
            .Select(x => x[2..])
            .ToList();
    }

    private static int Indent(string line) => line.Length - line.TrimStart().Length;

    private static string Escaped(string value) => value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("$", "$$", StringComparison.Ordinal);

    private static int Occurrences(string text, string value) => text.Split(value).Length - 1;
}
