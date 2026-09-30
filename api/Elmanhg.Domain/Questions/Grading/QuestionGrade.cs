namespace Elmanhg.Domain.Questions.Grading;

public sealed record QuestionGrade(decimal Score, decimal NormalisedScore, GradeOutcome Outcome, GradeFeedback? Feedback)
{
    // Scores display to 2 decimals; the normalised score keeps 4 so mastery thresholds (PRD §7.3) compare 1/3 and 2/3 precisely.
    private const int ScoreDecimals = 2;
    private const int NormalisedScoreDecimals = 4;

    public bool AwaitsReview => Feedback?.AwaitsReview == true;

    public static QuestionGrade FromNormalised(NormalisedGrade grade, int maxScore)
    {
        return new QuestionGrade(Math.Round(grade.Value * maxScore, ScoreDecimals, MidpointRounding.AwayFromZero), Math.Round(grade.Value, NormalisedScoreDecimals, MidpointRounding.AwayFromZero), ToOutcome(grade.Value), grade.Feedback);
    }

    public static GradeOutcome ToOutcome(decimal normalisedScore) => normalisedScore switch
    {
        >= 1m => GradeOutcome.Correct,
        > 0m => GradeOutcome.Partial,
        _ => GradeOutcome.Incorrect,
    };
}
