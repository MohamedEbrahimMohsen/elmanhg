namespace Elmanhg.Domain.Questions.Grading;

public static class GradeReviewScore
{
    // Matches QuestionGrade's 4 decimals so mastery thresholds compare a teacher score like an AI score.
    private const int NormalisedScoreDecimals = 4;

    public static decimal Normalise(decimal score, int maxScore) => Math.Round(score / maxScore, NormalisedScoreDecimals, MidpointRounding.AwayFromZero);
}
