using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;
using System.Globalization;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class TextGraderTests
{
    [Fact]
    public void GradeFill_AllBlanksRight_ReturnsOneWithoutFeedback()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("2", "5"))).Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void GradeFill_OneOfTwoBlanksRight_ReturnsHalfWithBlankTally()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("2", "7"))).Should().Be(new NormalisedGrade(0.5m, GradeFeedback.BlankTally(1, 2)));
    }

    [Fact]
    public void GradeFill_NoBlankRight_ReturnsZeroWithBlankTally()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "7"), ("2", "x"))).Should().Be(new NormalisedGrade(0m, GradeFeedback.BlankTally(0, 2)));
    }

    [Fact]
    public void GradeFill_OtherBlankEmpty_CountsAsMiss()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("2", "  "))).Should().Be(new NormalisedGrade(0.5m, GradeFeedback.BlankTally(1, 2)));
    }

    [Fact]
    public void GradeFill_ThreeBlanksTwoRight_ReturnsTwoThirdsWithTally()
    {
        var spec = new FillGradingSpec([new FillBlankAnswers("1", ["a"]), new FillBlankAnswers("2", ["b"]), new FillBlankAnswers("3", ["c"])]);

        TextGrader.GradeFill(spec, Fill(("1", "a"), ("2", "b"), ("3", "z"))).Should().Be(new NormalisedGrade(2m / 3m, GradeFeedback.BlankTally(2, 3)));
    }

    [Fact]
    public void GradeFill_SingleBlankWrong_ReturnsZeroWithoutFeedback()
    {
        TextGrader.GradeFill(Capital(AnswerNormalization.Default), Fill(("1", "الجيزة"))).Should().Be(new NormalisedGrade(0m, null));
    }

    [Fact]
    public void GradeFill_SecondAcceptedAnswerOfBlank_Matches()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("2", "Five"))).Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void GradeFill_AnswerAcceptedOnlyForOtherBlank_DoesNotMatch()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "5"), ("2", "20"))).Should().Be(new NormalisedGrade(0m, GradeFeedback.BlankTally(0, 2)));
    }

    [Fact]
    public void GradeFill_RepeatedResponseId_UsesFirstResponse()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("1", "7"), ("2", "5"))).Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void GradeFill_SpellingVariantWithUnifyOn_Matches()
    {
        TextGrader.GradeFill(Capital(AnswerNormalization.Default), Fill(("1", "القاهره"))).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeFill_SpellingVariantWithUnifyOff_DoesNotMatch()
    {
        TextGrader.GradeFill(Capital(new AnswerNormalization(UnifyTaaMarbuta: false)), Fill(("1", "القاهره"))).Value.Should().Be(0m);
    }

    [Fact]
    public void GradeFill_AnswerWithByteSwappedBom_ReturnsOne()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20\uFFFE"), ("2", "5"))).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeFill_EmptyAnswer_ReturnsUnanswered()
    {
        var spec = new FillGradingSpec([new FillBlankAnswers("1", [" "])]);

        TextGrader.GradeFill(spec, Fill(("1", "  "))).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeFill_NoBlanksInAnswer_ReturnsUnanswered()
    {
        TextGrader.GradeFill(TwoBlanks(), new FillAnswer(null)).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeFill_OnlyUnknownBlankIds_ReturnsUnanswered()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("9", "20"))).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeFill_NullNormalization_UsesDefaultRules()
    {
        var spec = new FillGradingSpec([new FillBlankAnswers("1", ["القاهرة"])], null);

        TextGrader.GradeFill(spec, Fill(("1", "القاهره"))).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericWithinAbsoluteTolerance_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("٩٫٧٥")).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericOutsideTolerance_ReturnsZero()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("9.6")).Should().Be(new NormalisedGrade(0m, null));
    }

    [Fact]
    public void GradeShort_NumericWithinPercentTolerance_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(200m, 5m, ToleranceMode.Percent), new ShortAnswer("209")).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData("9.8 m/s")]
    [InlineData("9.8e0")]
    [InlineData("1 000")]
    [InlineData("abc")]
    [InlineData("--1")]
    public void GradeShort_NumericUnparseable_ReturnsNotANumber(string text)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Should().Be(new NormalisedGrade(0m, GradeFeedback.NotANumber));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u200F")]
    public void GradeShort_NumericEmptyAnswer_ReturnsUnanswered(string? text)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Should().Be(NormalisedGrade.Unanswered);
    }

    [Theory]
    [InlineData("-79228162514264337593543950335")]
    [InlineData("79228162514264337593543950335")]
    public void GradeShort_NumericAnswerAtDecimalLimit_ReturnsZero(string text)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Value.Should().Be(0m);
    }

    [Theory]
    [InlineData("9.7")]
    [InlineData("9.9")]
    public void GradeShort_NumericAtAbsoluteBounds_IsInclusive(string text)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Should().Be(new NormalisedGrade(1m, null));
    }

    [Theory]
    [InlineData("9.69")]
    [InlineData("9.91")]
    public void GradeShort_NumericJustOutsideAbsoluteBounds_ReturnsZero(string text)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Should().Be(new NormalisedGrade(0m, null));
    }

    [Theory]
    [InlineData("190")]
    [InlineData("210")]
    public void GradeShort_NumericAtPercentBounds_IsInclusive(string text)
    {
        TextGrader.GradeShort(Numeric(200m, 5m, ToleranceMode.Percent), new ShortAnswer(text)).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData("189.99")]
    [InlineData("210.01")]
    public void GradeShort_NumericJustOutsidePercentBounds_ReturnsZero(string text)
    {
        TextGrader.GradeShort(Numeric(200m, 5m, ToleranceMode.Percent), new ShortAnswer(text)).Value.Should().Be(0m);
    }

    [Theory]
    [InlineData("-190", true)]
    [InlineData("\u2212210", true)]
    [InlineData("-189", false)]
    [InlineData("190", false)]
    public void GradeShort_NegativeValueWithPercentTolerance_UsesMagnitude(string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(-200m, 5m, ToleranceMode.Percent), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("-9.9", true)]
    [InlineData("-9.7", true)]
    [InlineData("9.8", false)]
    public void GradeShort_NegativeValueWithAbsoluteTolerance_IsInclusive(string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(-9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("-0", true)]
    [InlineData("0.000", true)]
    [InlineData("0.001", false)]
    [InlineData("-0.001", false)]
    public void GradeShort_ZeroValueWithPercentTolerance_AcceptsOnlyZero(string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(0m, 10m, ToleranceMode.Percent), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("9.80", true)]
    [InlineData("9.81", false)]
    public void GradeShort_ZeroTolerance_RequiresExactValue(string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0m, ToleranceMode.Absolute), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335", true)]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950334", true)]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950333", false)]
    public void GradeShort_SpecValueAtDecimalMax_DoesNotOverflow(string value, string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(Parse(value), 1m, ToleranceMode.Absolute), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("-79228162514264337593543950335", "-79228162514264337593543950335", true)]
    [InlineData("-79228162514264337593543950335", "-79228162514264337593543950333", false)]
    public void GradeShort_SpecValueAtDecimalMin_DoesNotOverflow(string value, string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(Parse(value), 1m, ToleranceMode.Absolute), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("-79228162514264337593543950335")]
    [InlineData("79228162514264337593543950335")]
    public void GradeShort_AbsoluteToleranceAtDecimalMax_AcceptsEveryNumber(string text)
    {
        TextGrader.GradeShort(Numeric(0m, decimal.MaxValue, ToleranceMode.Absolute), new ShortAnswer(text)).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_PercentProductOverflows_SaturatesBounds()
    {
        TextGrader.GradeShort(Numeric(decimal.MaxValue, 200m, ToleranceMode.Percent), new ShortAnswer("-79228162514264337593543950335")).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData("-79228162514264337593543950335", "150", "-79228162514264337593543950335", true)]
    [InlineData("-79228162514264337593543950335", "150", "39000000000000000000000000000", true)]
    [InlineData("-79228162514264337593543950335", "150", "40000000000000000000000000000", false)]
    [InlineData("79228162514264337593543950335", "150", "-39000000000000000000000000000", true)]
    [InlineData("79228162514264337593543950335", "150", "-40000000000000000000000000000", false)]
    [InlineData("79228162514264337593543950335", "300", "-79228162514264337593543950335", true)]
    public void GradeShort_PercentProductOverflows_BandFollowsValueSign(string value, string tolerance, string text, bool correct)
    {
        TextGrader.GradeShort(Numeric(Parse(value), Parse(tolerance), ToleranceMode.Percent), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("39614081257132168796771975167")]
    [InlineData("39614081257132168796771975168")]
    public void GradeShort_PercentNearOverflowThreshold_DoesNotThrow(string value)
    {
        TextGrader.GradeShort(Numeric(Parse(value), 200m, ToleranceMode.Percent), new ShortAnswer("-39614081257132168796771975167")).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData(ToleranceMode.Absolute, "9.8", true)]
    [InlineData(ToleranceMode.Percent, "9.8", true)]
    [InlineData(ToleranceMode.Absolute, "9.79", false)]
    public void GradeShort_NegativeToleranceInStoredSpec_TreatedAsZero(ToleranceMode mode, string text, bool correct)
    {
        TextGrader.GradeShort(new ShortGradingSpec(9.8m, -1m, mode, null, null), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Theory]
    [InlineData("9.8", true)]
    [InlineData("9.81", false)]
    public void GradeShort_MissingToleranceAndMode_RequiresExactValue(string text, bool correct)
    {
        TextGrader.GradeShort(new ShortGradingSpec(9.8m, null, null, null, null), new ShortAnswer(text)).Value.Should().Be(correct ? 1m : 0m);
    }

    [Fact]
    public void GradeShort_TextAcceptedAfterNormalisation_ReturnsOne()
    {
        TextGrader.GradeShort(Text("ماء"), new ShortAnswer(" مَاء ")).Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void GradeShort_TextNotAccepted_ReturnsZero()
    {
        TextGrader.GradeShort(Text("ماء"), new ShortAnswer("هواء")).Should().Be(new NormalisedGrade(0m, null));
    }

    [Fact]
    public void GradeShort_TextMatchesAnyAcceptedAnswer()
    {
        var spec = new ShortGradingSpec(null, null, null, ["ماء", "H2O"], AnswerNormalization.Default);

        TextGrader.GradeShort(spec, new ShortAnswer("h2o")).Should().Be(new NormalisedGrade(1m, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u064E")]
    public void GradeShort_TextEmptyAnswer_ReturnsUnanswered(string? text)
    {
        TextGrader.GradeShort(Text("ماء"), new ShortAnswer(text)).Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void GradeShort_NumericWithArabicThousandsSeparator_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(1000m, 0m, ToleranceMode.Absolute), new ShortAnswer("١٬٠٠٠")).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericWithRightToLeftMark_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("\u200F٩٫٧٥")).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericWithArabicComma_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0m, ToleranceMode.Absolute), new ShortAnswer("٩،٨")).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericIgnoresLetterRules_ParsesArabicDigits()
    {
        var spec = new ShortGradingSpec(9.8m, 0.1m, ToleranceMode.Absolute, null, new AnswerNormalization(false, false, false, false, false, false, false, false));

        TextGrader.GradeShort(spec, new ShortAnswer("٩٫٨")).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_TextNullNormalization_UsesDefaultRules()
    {
        var spec = new ShortGradingSpec(null, null, null, ["القاهرة"], null);

        TextGrader.GradeShort(spec, new ShortAnswer("القاهره")).Value.Should().Be(1m);
    }

    [Fact]
    public void GradeShort_TextFoldCaseOff_DoesNotMatchOtherCase()
    {
        var spec = new ShortGradingSpec(null, null, null, ["newton"], new AnswerNormalization(FoldCase: false));

        TextGrader.GradeShort(spec, new ShortAnswer("Newton")).Value.Should().Be(0m);
    }

    [Fact]
    public void GradeShort_InvisibleControlInsideHamza_MatchesComposedAccepted()
    {
        var spec = new ShortGradingSpec(null, null, null, ["\u0623"], new AnswerNormalization(UnifyAlef: false));

        TextGrader.GradeShort(spec, new ShortAnswer("\u0627\u200C\u0654")).Value.Should().Be(1m);
    }

    private static FillGradingSpec TwoBlanks()
    {
        return new FillGradingSpec([new FillBlankAnswers("1", ["20"]), new FillBlankAnswers("2", ["5", "five"])]);
    }

    private static FillGradingSpec Capital(AnswerNormalization normalization)
    {
        return new FillGradingSpec([new FillBlankAnswers("1", ["القاهرة"])], normalization);
    }

    private static FillAnswer Fill(params (string Id, string Text)[] blanks)
    {
        return new FillAnswer(blanks.Select(x => new FillBlankResponse(x.Id, x.Text)).ToList());
    }

    private static ShortGradingSpec Numeric(decimal value, decimal tolerance, ToleranceMode mode)
    {
        return new ShortGradingSpec(value, tolerance, mode, null, null);
    }

    private static ShortGradingSpec Text(string accepted)
    {
        return new ShortGradingSpec(null, null, null, [accepted], AnswerNormalization.Default);
    }

    private static decimal Parse(string value)
    {
        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }
}
