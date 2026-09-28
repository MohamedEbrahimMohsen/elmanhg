using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.Sessions.Exams;

public static class ExamDifficultyTargets
{
    // Difficulty mix values are whole percentages of the exam.
    private const int PercentTotal = 100;

    public static Dictionary<QuestionDifficulty, int> Apportion(int count, ExamDifficultyMix mix)
    {
        (QuestionDifficulty Difficulty, int Exact)[] shares = [(QuestionDifficulty.Easy, count * mix.EasyPercent), (QuestionDifficulty.Medium, count * mix.MediumPercent), (QuestionDifficulty.Hard, count * mix.HardPercent)];
        var targets = shares.ToDictionary(x => x.Difficulty, x => x.Exact / PercentTotal);
        var receivers = shares
            .OrderByDescending(x => x.Exact % PercentTotal)
            .ThenBy(x => x.Difficulty)
            .Take(count - targets.Values.Sum())
            .ToList();
        foreach (var share in receivers)
        {
            targets[share.Difficulty]++;
        }

        return targets;
    }
}
