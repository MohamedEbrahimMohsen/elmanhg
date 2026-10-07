using Core.DDD.Time;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    // Scores display to 2 decimals, matching QuestionGrade.
    private const int ScorePercentDecimals = 2;

    public void Submit()
    {
        if (IsExam)
        {
            throw new InvalidOperationException("Exams are submitted with SubmitExam.");
        }

        if (IsSubmitted)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.TruncateToMicroseconds();
        ScorePercent = CalculateScorePercent();
        SubmittedAt = now;
        Touch(now);
    }

    private decimal CalculateScorePercent() => Math.Round(Attempts.Sum(x => x.Score) * 100m / Items.Sum(x => x.MaxScore), ScorePercentDecimals, MidpointRounding.AwayFromZero);
}
