using Core.Logging;
using FluentAssertions;
using System.Text.RegularExpressions;

namespace Elmanhg.Tests.Core.Logging;

public sealed class TextRedactorTests
{
    [Fact]
    public void Redact_RulesInOrder_AppliesEachReplacement()
    {
        var redactor = new TextRedactor([new(new Regex("a", RegexOptions.NonBacktracking), "X"), new(new Regex("X", RegexOptions.NonBacktracking), "Y")]);

        var redacted = redactor.Redact("a b");

        redacted.Should().Be("Y b");
    }

    [Fact]
    public void Redact_Null_ReturnsNull()
    {
        var redactor = new TextRedactor([new(RedactionPatterns.EmailAddress, "[e]")]);

        var redacted = redactor.Redact(null);

        redacted.Should().BeNull();
    }

    [Fact]
    public void Redact_NoMatch_ReturnsInputUnchanged()
    {
        var redactor = new TextRedactor([new(RedactionPatterns.EmailAddress, "[e]")]);

        var redacted = redactor.Redact("plain text");

        redacted.Should().Be("plain text");
    }

    [Theory]
    [InlineData("mona@example.com")]
    [InlineData("mona%40example.com")]
    public void EmailAddressPattern_LiteralOrUrlEncoded_IsReplaced(string value)
    {
        var redacted = new TextRedactor([new(RedactionPatterns.EmailAddress, "[e]")]).Redact(value);

        redacted.Should().Be("[e]");
    }
}
