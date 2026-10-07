using Elmanhg.Application.Shared.Observability;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class LogRedactorBoundaryTests
{
    [Theory]
    [InlineData("رقم010 1234 5678", "رقم[redacted-phone]")]
    [InlineData("رقم01012345678", "رقم[redacted-phone]")]
    [InlineData("010 1234 5678رقم", "[redacted-phone]رقم")]
    [InlineData("01012345678رقم", "[redacted-phone]رقم")]
    [InlineData("اتصل على 010 1234 5678 الآن", "اتصل على [redacted-phone] الآن")]
    public void Redact_ArabicTextAdjacentToNumber_ReplacesNumberOnly(string text, string expected)
    {
        LogRedactor.Redact(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("call 010 1234 5678 011 1234 5678", "call [redacted-phone] [redacted-phone]")]
    [InlineData("call 01012345678,01112345678", "call [redacted-phone],[redacted-phone]")]
    [InlineData("call 010-1234-5678,011-1234-5678,012-1234-5678", "call [redacted-phone],[redacted-phone],[redacted-phone]")]
    public void Redact_NumbersOneSeparatorApart_ReplacesEach(string text, string expected)
    {
        LogRedactor.Redact(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("call 010 1234 5678\n", "call [redacted-phone]\n")]
    [InlineData("call 01012345678\r\n", "call [redacted-phone]\r\n")]
    [InlineData("tel:+201012345678;", "tel:[redacted-phone];")]
    [InlineData("00201012345678", "[redacted-phone]")]
    [InlineData("call 0020 10 1234 5678", "call [redacted-phone]")]
    public void Redact_NumberFollowedByPunctuation_KeepsTheFollowingCharacter(string text, string expected)
    {
        LogRedactor.Redact(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("at 2026-10-07T08:00:00.123Z")]
    [InlineData("order 1012345678901")]
    [InlineData("order 101 2345 67890")]
    [InlineData("user 3f2a1b10-1012-3456-9abc-def012345678")]
    [InlineData("user 3f2a1b10-1012-3456-9abc-a01012345678")]
    [InlineData("x01012345678")]
    public void Redact_TimestampsAndLongerDigitRuns_ReturnsUnchanged(string text)
    {
        LogRedactor.Redact(text).Should().Be(text);
    }
}
