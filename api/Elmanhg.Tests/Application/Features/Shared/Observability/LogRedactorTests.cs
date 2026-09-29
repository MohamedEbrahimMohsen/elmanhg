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
