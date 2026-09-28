using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class TextGraderTests
{
    [Fact]
    public void GradeFill_AllBlanksRight_ReturnsOne()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("2", "5"))).Should().Be(1m);
    }

    [Fact]
    public void GradeFill_OneOfTwoBlanksRight_ReturnsHalf()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20"), ("2", "7"))).Should().Be(0.5m);
    }

    [Fact]
    public void GradeFill_SpellingVariantWithUnifyOn_Matches()
    {
        TextGrader.GradeFill(Capital(AnswerNormalization.Default), Fill(("1", "القاهره"))).Should().Be(1m);
    }

    [Fact]
    public void GradeFill_SpellingVariantWithUnifyOff_DoesNotMatch()
    {
        TextGrader.GradeFill(Capital(new AnswerNormalization(UnifyTaaMarbuta: false)), Fill(("1", "القاهره"))).Should().Be(0m);
    }

    [Fact]
    public void GradeFill_AnswerWithByteSwappedBom_ReturnsOne()
    {
        TextGrader.GradeFill(TwoBlanks(), Fill(("1", "20\uFFFE"), ("2", "5"))).Should().Be(1m);
    }

    [Fact]
    public void GradeFill_EmptyAnswer_DoesNotMatch()
    {
        var spec = new FillGradingSpec([new FillBlankAnswers("1", [" "])]);

        TextGrader.GradeFill(spec, Fill(("1", "  "))).Should().Be(0m);
    }

    [Fact]
    public void GradeShort_NumericWithinAbsoluteTolerance_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("٩٫٧٥")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericOutsideTolerance_ReturnsZero()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("9.6")).Should().Be(0m);
    }

    [Fact]
    public void GradeShort_NumericWithinPercentTolerance_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(200m, 5m, ToleranceMode.Percent), new ShortAnswer("209")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericNotANumber_ReturnsZero()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("9.8 m/s")).Should().Be(0m);
    }

    [Theory]
    [InlineData("-79228162514264337593543950335")]
    [InlineData("79228162514264337593543950335")]
    public void GradeShort_NumericAnswerAtDecimalLimit_ReturnsZero(string text)
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer(text)).Should().Be(0m);
    }

    [Fact]
    public void GradeShort_TextAcceptedAfterNormalisation_ReturnsOne()
    {
        TextGrader.GradeShort(Text("ماء"), new ShortAnswer(" مَاء ")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_TextNotAccepted_ReturnsZero()
    {
        TextGrader.GradeShort(Text("ماء"), new ShortAnswer("هواء")).Should().Be(0m);
    }

    [Fact]
    public void GradeShort_NumericWithExponent_ReturnsZero()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("9.8e0")).Should().Be(0m);
    }

    [Fact]
    public void GradeShort_NumericWithArabicThousandsSeparator_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(1000m, 0m, ToleranceMode.Absolute), new ShortAnswer("١٬٠٠٠")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericWithRightToLeftMark_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0.1m, ToleranceMode.Absolute), new ShortAnswer("\u200F٩٫٧٥")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericWithArabicComma_ReturnsOne()
    {
        TextGrader.GradeShort(Numeric(9.8m, 0m, ToleranceMode.Absolute), new ShortAnswer("٩،٨")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_NumericIgnoresLetterRules_ParsesArabicDigits()
    {
        var spec = new ShortGradingSpec(9.8m, 0.1m, ToleranceMode.Absolute, null, new AnswerNormalization(false, false, false, false, false, false, false, false));

        TextGrader.GradeShort(spec, new ShortAnswer("٩٫٨")).Should().Be(1m);
    }

    [Fact]
    public void GradeFill_NullNormalization_UsesDefaultRules()
    {
        var spec = new FillGradingSpec([new FillBlankAnswers("1", ["القاهرة"])], null);

        TextGrader.GradeFill(spec, Fill(("1", "القاهره"))).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_TextNullNormalization_UsesDefaultRules()
    {
        var spec = new ShortGradingSpec(null, null, null, ["القاهرة"], null);

        TextGrader.GradeShort(spec, new ShortAnswer("القاهره")).Should().Be(1m);
    }

    [Fact]
    public void GradeShort_TextFoldCaseOff_DoesNotMatchOtherCase()
    {
        var spec = new ShortGradingSpec(null, null, null, ["newton"], new AnswerNormalization(FoldCase: false));

        TextGrader.GradeShort(spec, new ShortAnswer("Newton")).Should().Be(0m);
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
}
