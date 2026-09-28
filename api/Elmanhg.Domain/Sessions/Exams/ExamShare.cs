namespace Elmanhg.Domain.Sessions.Exams;

public sealed record ExamShare(Guid Id, Guid LessonId, int LessonOrder, int ObjectiveOrder, int QuestionCount, int CorrectCount, decimal Score, int MaxScore)
{
    // Matches the session score precision.
    private const int PercentDecimals = 2;

    public decimal ScorePercent => Percent(Score, MaxScore);

    public static decimal Percent(decimal score, int maxScore) => maxScore == 0 ? 0m : Math.Round(score * 100m / maxScore, PercentDecimals, MidpointRounding.AwayFromZero);
}
