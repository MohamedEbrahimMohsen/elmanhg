using Elmanhg.Domain.Questions.Schemas;
using System.Globalization;

namespace Elmanhg.Domain.Questions.Grading;

public static class TextGrader
{
    // Students type these separators on Arabic keyboards; they mean the ASCII characters.
    private const char ArabicDecimalSeparator = '٫';
    private const char MinusSign = '−';
    private const decimal PercentDivisor = 100m;

    public static decimal GradeFill(FillGradingSpec spec, FillAnswer answer)
    {
        var blanks = spec.Blanks ?? [];
        if (blanks.Count == 0)
        {
            return 0m;
        }

        var responses = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var response in answer.Blanks ?? [])
        {
            if (response?.Id is not null)
            {
                responses.TryAdd(response.Id, response.Text);
            }
        }

        var hits = blanks.Count(x => x.Id is not null && responses.TryGetValue(x.Id, out var response) && Matches(response, x.AcceptedAnswers ?? [], spec.UnifyLetterVariants));
        return (decimal)hits / blanks.Count;
    }

    public static decimal GradeShort(ShortGradingSpec spec, ShortAnswer answer)
    {
        if (spec.Value is not null)
        {
            var allowed = AllowedDifference(spec);
            return TryParseNumber(answer.Text, out var number) && number >= spec.Value.Value - allowed && number <= spec.Value.Value + allowed ? 1m : 0m;
        }

        return Matches(answer.Text, spec.AcceptedAnswers ?? [], spec.UnifyLetterVariants ?? true) ? 1m : 0m;
    }

    private static decimal AllowedDifference(ShortGradingSpec spec)
    {
        return spec.ToleranceMode == ToleranceMode.Percent ? Math.Abs(spec.Value.GetValueOrDefault()) * spec.Tolerance.GetValueOrDefault() / PercentDivisor : spec.Tolerance.GetValueOrDefault();
    }

    private static bool Matches(string? answer, List<string> accepted, bool unifyLetterVariants)
    {
        var normalised = AnswerNormalizer.Normalize(answer, unifyLetterVariants);
        if (normalised.Length == 0)
        {
            return false;
        }

        return accepted.Any(x => AnswerNormalizer.Normalize(x, unifyLetterVariants) == normalised);
    }

    private static bool TryParseNumber(string? text, out decimal value)
    {
        var candidate = AnswerNormalizer.Normalize(text, false)
            .Replace(ArabicDecimalSeparator, '.')
            .Replace(',', '.')
            .Replace(MinusSign, '-');
        return decimal.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
