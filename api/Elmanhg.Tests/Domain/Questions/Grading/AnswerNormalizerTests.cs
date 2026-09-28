using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class AnswerNormalizerTests
{
    [Fact]
    public void Normalize_Tashkeel_IsStripped()
    {
        AnswerNormalizer.Normalize("مَاءٌ", unifyLetterVariants: true).Should().Be("ماء");
    }

    [Fact]
    public void Normalize_Tatweel_IsStripped()
    {
        AnswerNormalizer.Normalize("مـاء", unifyLetterVariants: true).Should().Be("ماء");
    }

    [Fact]
    public void Normalize_UnifyOn_UnifiesLetterVariants()
    {
        AnswerNormalizer.Normalize("أإآٱ ة ى", unifyLetterVariants: true).Should().Be("اااا ه ي");
    }

    [Fact]
    public void Normalize_UnifyOff_KeepsLetterVariants()
    {
        AnswerNormalizer.Normalize("إلى", unifyLetterVariants: false).Should().Be("إلى");
    }

    [Fact]
    public void Normalize_ArabicIndicAndExtendedDigits_BecomeAscii()
    {
        AnswerNormalizer.Normalize("٢٠ ۳", unifyLetterVariants: true).Should().Be("20 3");
    }

    [Fact]
    public void Normalize_Whitespace_IsCollapsedAndTrimmed()
    {
        AnswerNormalizer.Normalize("  a \t  b  ", unifyLetterVariants: true).Should().Be("a b");
    }

    [Fact]
    public void Normalize_LatinLetters_AreLowerCased()
    {
        AnswerNormalizer.Normalize("Newton", unifyLetterVariants: true).Should().Be("newton");
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        AnswerNormalizer.Normalize(null, unifyLetterVariants: true).Should().Be(string.Empty);
    }
}
