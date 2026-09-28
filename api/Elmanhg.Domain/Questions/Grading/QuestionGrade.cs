namespace Elmanhg.Domain.Questions.Grading;

public sealed record QuestionGrade(decimal Score, decimal NormalisedScore, GradeOutcome Outcome)
{
    // Scores display to 2 decimals; the normalised score keeps 4 so mastery thresholds (PRD §7.3) compare 1/3 and 2/3 precisely.
    private const int ScoreDecimals = 2;
    private const int NormalisedScoreDecimals = 4;

    public static QuestionGrade FromNormalised(decimal normalised, int maxScore)
    {
        var outcome = normalised switch
        {
            >= 1m => GradeOutcome.Correct,
            > 0m => GradeOutcome.Partial,
            _ => GradeOutcome.Incorrect,
        };
        return new QuestionGrade(Math.Round(normalised * maxScore, ScoreDecimals, MidpointRounding.AwayFromZero), Math.Round(normalised, NormalisedScoreDecimals, MidpointRounding.AwayFromZero), outcome);
    }
}
