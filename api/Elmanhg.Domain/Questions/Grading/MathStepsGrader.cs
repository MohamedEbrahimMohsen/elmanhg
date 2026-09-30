using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions.Grading;

public static class MathStepsGrader
{
    // The none/partial/full scale per model step, shared with the AI service contract.
    public const int MaxStepPoints = 2;

    // The steps weight is a percentage of the question score.
    public const int PercentScale = 100;

    public static NormalisedGrade Grade(MathAnswerVerdict? verdict) => verdict switch
    {
        null => NormalisedGrade.Unanswered,
        MathAnswerVerdict.Equivalent => new(1m, GradeFeedback.MathFinalAnswerOnly),
        MathAnswerVerdict.NotEquivalent => new(0m, GradeFeedback.MathFinalAnswerOnly),
        MathAnswerVerdict.WrongForm => new(0m, GradeFeedback.MathWrongForm),
        MathAnswerVerdict.Unreadable => new(0m, GradeFeedback.MathUnreadable),
        MathAnswerVerdict.Unchecked => new(0m, GradeFeedback.MathUnchecked),
        _ => throw new InvalidOperationException("Unsupported math verdict."),
    };

    public static bool NeedsStepGrading(MathStepsGradingSpec spec, MathStepsAnswer answer) => (spec.StepsWeight ?? 0) > 0 && spec.ModelSolution is { Count: > 0 } && (answer.Steps ?? []).Any(x => !string.IsNullOrWhiteSpace(x));

    public static NormalisedGrade Combine(MathStepsGradingSpec spec, MathAnswerVerdict verdict, IReadOnlyList<MathStepAward>? awards)
    {
        if (verdict == MathAnswerVerdict.Unchecked)
        {
            throw new InvalidOperationException("An unchecked final answer has no grade.");
        }

        var weight = spec.StepsWeight ?? 0;
        var modelSteps = spec.ModelSolution?.Count ?? 0;
        if (weight == 0 || modelSteps == 0)
        {
            return Grade(verdict);
        }

        var given = awards ?? Enumerable.Range(0, modelSteps).Select(x => new MathStepAward(x, 0)).ToList();
        if (!AreValid(given, modelSteps))
        {
            throw new InvalidOperationException("Math step awards do not match the model solution.");
        }

        var finalCredit = verdict == MathAnswerVerdict.Equivalent ? 1m : 0m;
        var stepCredit = (decimal)given.Sum(x => x.Points) / (MaxStepPoints * modelSteps);
        var value = (((PercentScale - weight) * finalCredit) + (weight * stepCredit)) / PercentScale;
        return new NormalisedGrade(value, GradeFeedback.MathStepTally(given.Count(x => x.Points == MaxStepPoints), modelSteps));
    }

    private static bool AreValid(IReadOnlyList<MathStepAward> awards, int modelSteps) => awards.Count == modelSteps
        && Enumerable.Range(0, modelSteps).All(index => awards.Count(x => x.StepIndex == index) == 1)
        && awards.All(x => x.Points is >= 0 and <= MaxStepPoints);
}
