using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.Sessions.Exams;

public static class ExamQuestionSelector
{
    public static List<Guid> Select(IReadOnlyCollection<ExamCandidate> candidates, IReadOnlySet<Guid> masteredQuestionIds, IReadOnlyList<ExamTypeCount> typeCounts, ExamDifficultyMix? difficultyMix, Random random)
    {
        var pool = candidates
            .DistinctBy(x => x.QuestionId)
            .OrderBy(x => x.QuestionId)
            .ToList();
        List<Guid> selected = [];
        foreach (var typeCount in typeCounts.Where(x => x.Count > 0).OrderBy(x => x.Type))
        {
            var ofType = pool
                .Where(x => x.Type == typeCount.Type)
                .ToList();
            List<ExamCandidate> picked = [];
            if (difficultyMix is not null)
            {
                var targets = ExamDifficultyTargets.Apportion(typeCount.Count, difficultyMix);
                foreach (var difficulty in Enum.GetValues<QuestionDifficulty>())
                {
                    picked.AddRange(Preferred(ofType.Where(x => x.Difficulty == difficulty && !picked.Contains(x)), masteredQuestionIds, random).Take(targets[difficulty]));
                }
            }

            picked.AddRange(Preferred(ofType.Where(x => !picked.Contains(x)), masteredQuestionIds, random).Take(typeCount.Count - picked.Count));
            selected.AddRange(picked
                .OrderBy(x => x.Difficulty)
                .Select(x => x.QuestionId));
        }

        return selected;
    }

    private static ExamCandidate[] Preferred(IEnumerable<ExamCandidate> list, IReadOnlySet<Guid> mastered, Random random)
    {
        var candidates = list.ToList();
        var notMastered = candidates
            .Where(x => !mastered.Contains(x.QuestionId))
            .ToArray();
        var masteredOnes = candidates
            .Where(x => mastered.Contains(x.QuestionId))
            .ToArray();
        random.Shuffle(notMastered);
        random.Shuffle(masteredOnes);
        return [.. notMastered, .. masteredOnes];
    }
}
