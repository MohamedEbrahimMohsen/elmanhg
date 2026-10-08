using Elmanhg.Application.Shared.Observability;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class LogRedactorArabicIndicDigitTests
{
    [Theory]
    [InlineData("call ٠١٠١٢٣٤٥٦٧٨")]
    [InlineData("call ۰۱۰۱۲۳۴۵۶۷۸")]
    [InlineData("call ٠١٠ ١٢٣٤ ٥٦٧٨")]
    [InlineData("call ٠١٠-١٢٣٤-٥٦٧٨")]
    [InlineData("call ٠١٠٠ ١٢٣ ٤٥٦٧")]
    [InlineData("call +٢٠١٠١٢٣٤٥٦٧٨")]
    [InlineData("call +٢٠ ١٠ ١٢٣٤ ٥٦٧٨")]
    [InlineData("call +٢٠-١٠٠-١٢٣-٤٥٦٧")]
    [InlineData("call ٠٠٢٠١٠١٢٣٤٥٦٧٨")]
    [InlineData("call ٠٠٢٠ ١٠ ١٢٣٤ ٥٦٧٨")]
    [InlineData("call ۰۱۲۴۴۵۵۶۶۷۷")]
    public void Redact_ArabicIndicMobileNumber_ReplacesWithMarker(string text)
    {
        LogRedactor.Redact(text).Should().Be("call [redacted-phone]");
    }

    [Theory]
    [InlineData("call 010١٢٣٤٥٦٧٨")]
    [InlineData("call ٠1٠1234٥678")]
    [InlineData("call ۰۱٠١٢٣٤۵۶۷۸")]
    [InlineData("call +20١٠١٢٣٤٥٦٧٨")]
    public void Redact_MixedDigitScripts_ReplacesWithMarker(string text)
    {
        LogRedactor.Redact(text).Should().Be("call [redacted-phone]");
    }

    [Theory]
    [InlineData("tel:+٢٠١٠١٢٣٤٥٦٧٨;", "tel:[redacted-phone];")]
    [InlineData("رقم٠١٠١٢٣٤٥٦٧٨", "رقم[redacted-phone]")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨رقم", "[redacted-phone]رقم")]
    [InlineData("رقم٠١٠١٢٣٤٥٦٧٨رقم", "رقم[redacted-phone]رقم")]
    [InlineData("اتصل على ٠١٠ ١٢٣٤ ٥٦٧٨ الآن", "اتصل على [redacted-phone] الآن")]
    [InlineData("رقمي٠١١١٢٣٤٥٦٧٨، شكرا", "رقمي[redacted-phone]، شكرا")]
    [InlineData("call ٠١٠١٢٣٤٥٦٧٨\n", "call [redacted-phone]\n")]
    public void Redact_ArabicTextOrPunctuationAdjacentToArabicIndicNumber_ReplacesNumberOnly(string text, string expected)
    {
        LogRedactor.Redact(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("٠١٠١٢٣٤٥٦٧٨,٠١١١٢٣٤٥٦٧٨", "[redacted-phone],[redacted-phone]")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨ ٠١٢١٢٣٤٥٦٧٨", "[redacted-phone] [redacted-phone]")]
    public void Redact_ArabicIndicNumbersOneSeparatorApart_ReplacesEach(string text, string expected)
    {
        LogRedactor.Redact(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("طلب ١٠١٢٣٤٥٦٧٨٩٠١")]
    [InlineData("x٠١٠١٢٣٤٥٦٧٨")]
    [InlineData("رمز ١٢ ٣٤ ٥٦")]
    [InlineData("٢٠٢٦-١٠-٠٧")]
    [InlineData("call ٠١٣١٢٣٤٥٦٧٨")]
    public void Redact_ArabicIndicNonMobileDigitRuns_ReturnsUnchanged(string text)
    {
        LogRedactor.Redact(text).Should().Be(text);
    }

    [Theory]
    [InlineData("9٠١٠١٢٣٤٥٦٧٨")]
    [InlineData("٩01012345678")]
    [InlineData("01012345678٩")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨9")]
    public void Redact_DigitOfAnotherScriptAdjacent_ReturnsUnchanged(string text)
    {
        LogRedactor.Redact(text).Should().Be(text);
    }
}
