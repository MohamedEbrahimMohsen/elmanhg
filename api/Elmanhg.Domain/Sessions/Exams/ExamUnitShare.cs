namespace Elmanhg.Domain.Sessions.Exams;

public sealed record ExamUnitShare(Guid UnitId, int QuestionCount, int CorrectCount, decimal Score, int MaxScore)
{
    public decimal ScorePercent => ExamShare.Percent(Score, MaxScore);
}
