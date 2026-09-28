namespace Elmanhg.Domain.Sessions.Exams;

public sealed record ExamShare(Guid Id, Guid LessonId, int LessonOrder, int ObjectiveOrder, int QuestionCount, int CorrectCount, decimal Score, int MaxScore)
{
    // Matches the session score precision.
    private const int PercentDecimals = 2;

    public decimal ScorePercent => MaxScore == 0 ? 0m : Math.Round(Score * 100m / MaxScore, PercentDecimals, MidpointRounding.AwayFromZero);
}
