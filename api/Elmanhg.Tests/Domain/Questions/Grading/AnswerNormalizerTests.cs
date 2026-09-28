using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class AnswerNormalizerTests
{
    private static readonly AnswerNormalization LettersOff = new(UnifyAlef: false, UnifyTaaMarbuta: false, UnifyAlefMaqsura: false);

    [Fact]
    public void Normalize_Tashkeel_IsStripped()
    {
        AnswerNormalizer.Normalize("مَاءٌ", AnswerNormalization.Default).Should().Be("ماء");
    }

    [Fact]
    public void Normalize_Tatweel_IsStripped()
    {
        AnswerNormalizer.Normalize("مـاء", AnswerNormalization.Default).Should().Be("ماء");
    }

    [Fact]
    public void Normalize_UnifyOn_UnifiesLetterVariants()
    {
        AnswerNormalizer.Normalize("أإآٱ ة ى", AnswerNormalization.Default).Should().Be("اااا ه ي");
    }

    [Fact]
    public void Normalize_UnifyOff_KeepsLetterVariants()
    {
        AnswerNormalizer.Normalize("إلى", LettersOff).Should().Be("إلى");
    }

    [Fact]
    public void Normalize_ArabicIndicAndExtendedDigits_BecomeAscii()
    {
        AnswerNormalizer.Normalize("٢٠ ۳", AnswerNormalization.Default).Should().Be("20 3");
    }

    [Fact]
    public void Normalize_Whitespace_IsCollapsedAndTrimmed()
    {
        AnswerNormalizer.Normalize("  a \t  b  ", AnswerNormalization.Default).Should().Be("a b");
    }

    [Fact]
    public void Normalize_LatinLetters_AreLowerCased()
    {
        AnswerNormalizer.Normalize("Newton", AnswerNormalization.Default).Should().Be("newton");
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        AnswerNormalizer.Normalize(null, AnswerNormalization.Default).Should().Be(string.Empty);
    }

    [Fact]
    public void Normalize_StripTashkeelOff_KeepsTashkeel()
    {
        AnswerNormalizer.Normalize("مَاء", new AnswerNormalization(StripTashkeel: false)).Should().Be("مَاء");
    }

    [Fact]
    public void Normalize_StripTatweelOff_KeepsTatweel()
    {
        AnswerNormalizer.Normalize("مـاء", new AnswerNormalization(StripTatweel: false)).Should().Be("مـاء");
    }

    [Fact]
    public void Normalize_UnifyAlefOff_KeepsAlefFormsButUnifiesTaaMarbuta()
    {
        AnswerNormalizer.Normalize("أسامة", new AnswerNormalization(UnifyAlef: false)).Should().Be("أسامه");
    }

    [Fact]
    public void Normalize_UnifyTaaMarbutaOff_KeepsTaaMarbutaButUnifiesAlef()
    {
        AnswerNormalizer.Normalize("أسامة", new AnswerNormalization(UnifyTaaMarbuta: false)).Should().Be("اسامة");
    }

    [Fact]
    public void Normalize_UnifyAlefMaqsuraOff_KeepsAlefMaqsura()
    {
        AnswerNormalizer.Normalize("مصطفى", new AnswerNormalization(UnifyAlefMaqsura: false)).Should().Be("مصطفى");
    }

    [Fact]
    public void Normalize_ConvertDigitsOff_KeepsArabicIndicDigits()
    {
        AnswerNormalizer.Normalize("٢٠ ۳", new AnswerNormalization(ConvertDigits: false)).Should().Be("٢٠ ۳");
    }

    [Fact]
    public void Normalize_CollapseWhitespaceOff_KeepsInnerWhitespaceAndTrims()
    {
        AnswerNormalizer.Normalize("  a \t b  ", new AnswerNormalization(CollapseWhitespace: false)).Should().Be("a \t b");
    }

    [Fact]
    public void Normalize_FoldCaseOff_KeepsCase()
    {
        AnswerNormalizer.Normalize("Newton", new AnswerNormalization(FoldCase: false)).Should().Be("Newton");
    }

    [Theory]
    [InlineData("أ", "أ")]
    [InlineData("إ", "إ")]
    [InlineData("آ", "آ")]
    public void Normalize_DecomposedHamzaOrMadda_ComposesWithUnifyAlefOff(string text, string expected)
    {
        AnswerNormalizer.Normalize(text, new AnswerNormalization(UnifyAlef: false)).Should().Be(expected);
    }

    [Theory]
    [InlineData('؜')]
    [InlineData('​')]
    [InlineData('‌')]
    [InlineData('‍')]
    [InlineData('‎')]
    [InlineData('‏')]
    [InlineData('‫')]
    [InlineData('‮')]
    [InlineData('⁠')]
    [InlineData('⁦')]
    [InlineData('⁩')]
    [InlineData('﻿')]
    public void Normalize_InvisibleControl_IsRemoved(char control)
    {
        AnswerNormalizer.Normalize($"م{control}اء", AnswerNormalization.Default).Should().Be("ماء");
    }

    [Fact]
    public void Normalize_ArabicComma_MapsToComma()
    {
        AnswerNormalizer.Normalize("أ، ب", AnswerNormalization.Default).Should().Be("ا, ب");
    }

    [Fact]
    public void Normalize_PersianYeh_MapsToYaaWithMaqsuraOff()
    {
        AnswerNormalizer.Normalize("علی", new AnswerNormalization(UnifyAlefMaqsura: false)).Should().Be("علي");
    }

    [Fact]
    public void Normalize_UnpairedSurrogate_IsDropped()
    {
        AnswerNormalizer.Normalize("a\uD800b", AnswerNormalization.Default).Should().Be("ab");
    }

    [Fact]
    public void Normalize_ByteSwappedBom_IsDropped()
    {
        AnswerNormalizer.Normalize("a\uFFFEb", AnswerNormalization.Default).Should().Be("ab");
    }

    [Fact]
    public void Normalize_AllRulesOff_AppliesOnlyAlwaysOnSteps()
    {
        var rules = new AnswerNormalization(false, false, false, false, false, false, false, false);

        AnswerNormalizer.Normalize(" Aـَ٢‏ ", rules).Should().Be("Aـَ٢");
    }

    [Theory]
    [InlineData("\u0627\u200C\u0654", "\u0623")]
    [InlineData("\u0627\u200F\u0654", "\u0623")]
    [InlineData("\u0627\u2060\u0653", "\u0622")]
    [InlineData("\u0648\uFEFF\u0654", "\u0624")]
    public void Normalize_InvisibleControlBetweenLetterAndMark_Composes(string text, string expected)
    {
        AnswerNormalizer.Normalize(text, new AnswerNormalization(StripTashkeel: false, UnifyAlef: false)).Should().Be(expected);
    }

    [Fact]
    public void Normalize_InvisibleControlBetweenLetterAndMark_ComposesWithUnifyAlefOff()
    {
        AnswerNormalizer.Normalize("\u0627\u200C\u0654", new AnswerNormalization(UnifyAlef: false)).Should().Be("\u0623");
    }

    [Theory]
    [InlineData("\u0627\u200C\u0654")]
    [InlineData("\u0627\u200F\u0654")]
    [InlineData("\u0627\u2060\u0653")]
    [InlineData("\u0648\uFEFF\u0654")]
    [InlineData("\u0645\u200F\u0627\u0621")]
    [InlineData("  A\u0640\u064E\u0662\u200F ")]
    public void Normalize_IsIdempotent(string text)
    {
        var rules = new AnswerNormalization(StripTashkeel: false, UnifyAlef: false);
        var once = AnswerNormalizer.Normalize(text, rules);

        AnswerNormalizer.Normalize(once, rules).Should().Be(once);
    }
}
