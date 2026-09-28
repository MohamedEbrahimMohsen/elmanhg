using Elmanhg.Domain.Questions.Schemas;
using System.Globalization;

namespace Elmanhg.Domain.Questions.Grading;

public static class TextGrader
{
    // Students type these separators on Arabic keyboards; ٬ groups thousands, the others mean the ASCII characters.
    private const char ArabicDecimalSeparator = '\u066B';
    private const char ArabicThousandsSeparator = '\u066C';
    private const char MinusSign = '\u2212';
    private const NumberStyles PlainDecimal = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
    private const decimal PercentDivisor = 100m;
    private static readonly AnswerNormalization NumberRules = new(UnifyAlef: false, UnifyTaaMarbuta: false, UnifyAlefMaqsura: false);

    public static NormalisedGrade GradeFill(FillGradingSpec spec, FillAnswer answer)
    {
        var blanks = spec.Blanks ?? [];
        if (blanks.Count == 0)
        {
            return new NormalisedGrade(0m, null);
        }

        var rules = spec.Normalization ?? AnswerNormalization.Default;
        var responses = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var response in answer.Blanks ?? [])
        {
            if (response?.Id is not null)
            {
                responses.TryAdd(response.Id, response.Text);
            }
        }

        var normalised = blanks
            .Select(x => x.Id is not null && responses.TryGetValue(x.Id, out var text) ? AnswerNormalizer.Normalize(text, rules) : string.Empty)
            .ToList();
        if (normalised.All(x => x.Length == 0))
        {
            return NormalisedGrade.Unanswered;
        }

        var hits = blanks
            .Where((blank, index) => IsAccepted(normalised[index], blank.AcceptedAnswers, rules))
            .Count();
        if (hits == blanks.Count)
        {
            return new NormalisedGrade(1m, null);
        }

        return new NormalisedGrade((decimal)hits / blanks.Count, blanks.Count > 1 ? GradeFeedback.BlankTally(hits, blanks.Count) : null);
    }

    public static NormalisedGrade GradeShort(ShortGradingSpec spec, ShortAnswer answer)
    {
        if (spec.Value is not null)
        {
            return GradeNumeric(spec.Value.Value, spec.Tolerance, spec.ToleranceMode, answer.Text);
        }

        var rules = spec.Normalization ?? AnswerNormalization.Default;
        var normalised = AnswerNormalizer.Normalize(answer.Text, rules);
        if (normalised.Length == 0)
        {
            return NormalisedGrade.Unanswered;
        }

        return new NormalisedGrade(IsAccepted(normalised, spec.AcceptedAnswers, rules) ? 1m : 0m, null);
    }

    // The student's number is only compared, never an arithmetic operand, so no answer can overflow grading.
    private static NormalisedGrade GradeNumeric(decimal value, decimal? tolerance, ToleranceMode? mode, string? text)
    {
        var normalised = AnswerNormalizer.Normalize(text, NumberRules);
        if (normalised.Length == 0)
        {
            return NormalisedGrade.Unanswered;
        }

        if (!TryParseNumber(normalised, out var number))
        {
            return new NormalisedGrade(0m, GradeFeedback.NotANumber);
        }

        var (lower, upper) = Bounds(value, Math.Max(0m, tolerance.GetValueOrDefault()), mode);
        return new NormalisedGrade(number >= lower && number <= upper ? 1m : 0m, null);
    }

    private static (decimal Lower, decimal Upper) Bounds(decimal value, decimal amount, ToleranceMode? mode)
    {
        if (mode != ToleranceMode.Percent)
        {
            return Around(value, amount);
        }

        var fraction = amount / PercentDivisor;
        var magnitude = Math.Abs(value);
        if (fraction <= 1m || magnitude < decimal.MaxValue / fraction)
        {
            return Around(value, magnitude * fraction);
        }

        var near = -SaturatingProduct(magnitude, fraction - 1m);
        return value < 0m ? (decimal.MinValue, -near) : (near, decimal.MaxValue);
    }

    // allowed is never negative, so MinValue + allowed and MaxValue - allowed cannot overflow.
    private static (decimal Lower, decimal Upper) Around(decimal value, decimal allowed)
    {
        var lower = value < decimal.MinValue + allowed ? decimal.MinValue : value - allowed;
        var upper = value > decimal.MaxValue - allowed ? decimal.MaxValue : value + allowed;
        return (lower, upper);
    }

    private static decimal SaturatingProduct(decimal magnitude, decimal factor)
    {
        return factor > 1m && magnitude >= decimal.MaxValue / factor ? decimal.MaxValue : magnitude * factor;
    }

    private static bool IsAccepted(string normalisedAnswer, List<string>? accepted, AnswerNormalization rules)
    {
        return normalisedAnswer.Length > 0 && (accepted ?? []).Any(x => AnswerNormalizer.Normalize(x, rules) == normalisedAnswer);
    }

    private static bool TryParseNumber(string normalised, out decimal value)
    {
        var candidate = normalised
            .Replace(ArabicThousandsSeparator.ToString(), string.Empty)
            .Replace(ArabicDecimalSeparator, '.')
            .Replace(',', '.')
            .Replace(MinusSign, '-');
        return decimal.TryParse(candidate, PlainDecimal, CultureInfo.InvariantCulture, out value);
    }
}
