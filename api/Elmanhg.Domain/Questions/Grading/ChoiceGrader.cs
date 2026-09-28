using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions.Grading;

public static class ChoiceGrader
{
    public static NormalisedGrade GradeMcq(McqGradingSpec spec, McqAnswer answer)
    {
        if (answer.OptionId is null)
        {
            return NormalisedGrade.Unanswered;
        }

        return new NormalisedGrade(string.Equals(answer.OptionId, spec.CorrectOptionId, StringComparison.Ordinal) ? 1m : 0m, null);
    }

    public static NormalisedGrade GradeTrueFalse(TrueFalseGradingSpec spec, TrueFalseAnswer answer)
    {
        if (answer.Value is null)
        {
            return NormalisedGrade.Unanswered;
        }

        return new NormalisedGrade(answer.Value == spec.CorrectAnswer ? 1m : 0m, null);
    }

    public static NormalisedGrade GradeMulti(MultiGradingSpec spec, MultiAnswer answer)
    {
        var selected = (answer.OptionIds ?? [])
            .Where(x => x is not null)
            .ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0)
        {
            return NormalisedGrade.Unanswered;
        }

        var correct = (spec.CorrectOptionIds ?? []).ToHashSet(StringComparer.Ordinal);
        if (correct.Count == 0)
        {
            return new NormalisedGrade(0m, null);
        }

        var right = selected.Count(correct.Contains);
        var wrong = selected.Count - right;
        if (right == correct.Count && wrong == 0)
        {
            return new NormalisedGrade(1m, null);
        }

        var value = spec.PartialCredit ? Math.Max(0m, (decimal)(right - wrong) / correct.Count) : 0m;
        return new NormalisedGrade(value, GradeFeedback.ChoiceTally(right, wrong, correct.Count));
    }
}
