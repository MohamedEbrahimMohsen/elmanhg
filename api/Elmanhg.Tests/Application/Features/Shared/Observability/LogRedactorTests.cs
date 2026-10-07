using Elmanhg.Application.Shared.Observability;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class LogRedactorTests
{
    [Fact]
    public void Redact_Email_ReplacesWithMarker()
    {
        LogRedactor.Redact("mail mona@example.com now").Should().Be("mail [redacted-email] now");
    }

    [Fact]
    public void Redact_UrlEncodedEmail_ReplacesWithMarker()
    {
        LogRedactor.Redact("/audit-logs?actor=mona%40example.com").Should().Be("/audit-logs?actor=[redacted-email]");
    }

    [Fact]
    public void Redact_LocalMobileNumber_ReplacesWithMarker()
    {
        LogRedactor.Redact("call 01012345678").Should().Be("call [redacted-phone]");
    }

    [Fact]
    public void Redact_InternationalMobileNumber_ReplacesWithMarker()
    {
        LogRedactor.Redact("call +201012345678").Should().Be("call [redacted-phone]");
    }

    [Theory]
    [InlineData("+20 10 1234 5678")]
    [InlineData("010-1234-5678")]
    [InlineData("010 1234 5678")]
    [InlineData("+20-100-123-4567")]
    [InlineData("0100 123 4567")]
    public void Redact_FormattedMobileNumber_ReplacesWithMarker(string phoneNumber)
    {
        LogRedactor.Redact("call " + phoneNumber).Should().Be("call [redacted-phone]");
    }

    [Theory]
    [InlineData("on 2026-10-07")]
    [InlineData("pin 12 34 56")]
    [InlineData("user 3f2a1b10-1234-5678-9abc-def012345678")]
    [InlineData("scores 10 12 15 20 11")]
    public void Redact_ShortDigitGroups_ReturnsUnchanged(string text)
    {
        LogRedactor.Redact(text).Should().Be(text);
    }

    [Fact]
    public void Redact_TextWithoutPersonalData_ReturnsUnchanged()
    {
        const string text = "session 3f2c9a7e-8b4d-4e6f-9a2b-5c7d8e9f0a3b code 482913";

        LogRedactor.Redact(text).Should().Be(text);
    }

    [Fact]
    public void Redact_Null_ReturnsNull()
    {
        LogRedactor.Redact(null).Should().BeNull();
    }
}
