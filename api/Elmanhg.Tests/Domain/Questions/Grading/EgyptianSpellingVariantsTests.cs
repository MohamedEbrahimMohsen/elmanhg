using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class EgyptianSpellingVariantsTests
{
    [Theory]
    [InlineData("القاهره", "القاهرة")]
    [InlineData("اسكندريه", "إسكندرية")]
    [InlineData("مصطفي", "مصطفى")]
    [InlineData("الوادى", "الوادي")]
    [InlineData("احمد", "أحمد")]
    [InlineData("ابراهيم", "إبراهيم")]
    [InlineData("القران", "القرآن")]
    [InlineData("العلم", "ٱلعلم")]
    [InlineData("مدرسه", "مَدْرَسَة")]
    [InlineData("جمـــال", "جمال")]
    [InlineData("2024", "٢٠٢٤")]
    [InlineData("  نهر   النيل ", "نهر النيل")]
    [InlineData("رئيس", "رئيس")]
    [InlineData("علی", "علي")]
    [InlineData("احمد, محمد", "أحمد، محمد")]
    [InlineData("‏ماء", "ماء")]
    [InlineData("NaCl", "nacl")]
    public void GradeShort_EgyptianVariantOfAcceptedAnswer_ReturnsOne(string answer, string accepted)
    {
        TextGrader.GradeShort(Spec(accepted), new ShortAnswer(answer)).Value.Should().Be(1m);
    }

    [Theory]
    [InlineData("القاهر", "القاهرة")]
    [InlineData("عل", "على")]
    [InlineData("مسئول", "مسؤول")]
    [InlineData("هواء", "ماء")]
    [InlineData("١٣", "12")]
    public void GradeShort_DifferentWord_ReturnsZero(string answer, string accepted)
    {
        TextGrader.GradeShort(Spec(accepted), new ShortAnswer(answer)).Value.Should().Be(0m);
    }

    private static ShortGradingSpec Spec(string accepted)
    {
        return new ShortGradingSpec(null, null, null, [accepted], AnswerNormalization.Default);
    }
}
