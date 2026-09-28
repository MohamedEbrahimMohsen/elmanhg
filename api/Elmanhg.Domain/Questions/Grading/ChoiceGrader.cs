using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions.Grading;

public static class ChoiceGrader
{
    public static decimal GradeMcq(McqGradingSpec spec, McqAnswer answer)
    {
        return answer.OptionId is not null && string.Equals(answer.OptionId, spec.CorrectOptionId, StringComparison.Ordinal) ? 1m : 0m;
    }

    public static decimal GradeTrueFalse(TrueFalseGradingSpec spec, TrueFalseAnswer answer)
    {
        return answer.Value is not null && answer.Value == spec.CorrectAnswer ? 1m : 0m;
    }

    public static decimal GradeMulti(MultiGradingSpec spec, MultiAnswer answer)
    {
        var correct = (spec.CorrectOptionIds ?? []).ToHashSet(StringComparer.Ordinal);
        if (correct.Count == 0)
        {
            return 0m;
        }

        var selected = (answer.OptionIds ?? [])
            .Where(x => x is not null)
            .ToHashSet(StringComparer.Ordinal);
        if (!spec.PartialCredit)
        {
            return selected.SetEquals(correct) ? 1m : 0m;
        }

        var right = selected.Count(correct.Contains);
        var wrong = selected.Count - right;
        return Math.Max(0m, (decimal)(right - wrong) / correct.Count);
    }
}
