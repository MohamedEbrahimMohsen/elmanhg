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
        TextGrader.GradeFill(Capital(unifyLetterVariants: true), Fill(("1", "القاهره"))).Should().Be(1m);
    }

    [Fact]
    public void GradeFill_SpellingVariantWithUnifyOff_DoesNotMatch()
    {
        TextGrader.GradeFill(Capital(unifyLetterVariants: false), Fill(("1", "القاهره"))).Should().Be(0m);
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

    private static FillGradingSpec TwoBlanks()
    {
        return new FillGradingSpec([new FillBlankAnswers("1", ["20"]), new FillBlankAnswers("2", ["5", "five"])]);
    }

    private static FillGradingSpec Capital(bool unifyLetterVariants)
    {
        return new FillGradingSpec([new FillBlankAnswers("1", ["القاهرة"])], unifyLetterVariants);
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
        return new ShortGradingSpec(null, null, null, [accepted], true);
    }
}
