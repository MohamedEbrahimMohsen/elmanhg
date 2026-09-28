namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    // Scores display to 2 decimals, matching QuestionGrade.
    private const int ScorePercentDecimals = 2;

    public void Submit()
    {
        if (IsSubmitted)
        {
            return;
        }

        var now = UtcNowToMicroseconds();
        var possible = Items.Sum(x => x.MaxScore);
        ScorePercent = Math.Round(Attempts.Sum(x => x.Score) * 100m / possible, ScorePercentDecimals, MidpointRounding.AwayFromZero);
        SubmittedAt = now;
        Touch(now);
    }
}
