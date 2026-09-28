namespace Elmanhg.Domain.Sessions.Selection;

public static class QuestionSelector
{
    public static List<Guid> Select(IReadOnlyCollection<Guid> candidateIds, IReadOnlyCollection<QuestionAttemptSummary> summaries, int count, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var candidates = candidateIds
            .Distinct()
            .Order()
            .ToList();
        var summariesByQuestion = summaries.ToDictionary(x => x.QuestionId);
        var unseen = candidates
            .Where(x => !summariesByQuestion.ContainsKey(x))
            .ToArray();
        var seen = candidates
            .Where(summariesByQuestion.ContainsKey)
            .Select(x => summariesByQuestion[x])
            .ToList();

        random.Shuffle(unseen);
        var lastWrong = ShuffledOldestFirst(seen.Where(x => x.Bucket == QuestionSelectionBucket.LastWrong), random)
            .Select(x => x.QuestionId)
            .ToList();
        var correctOnce = seen
            .Where(x => x.Bucket == QuestionSelectionBucket.CorrectOnce)
            .Select(x => x.QuestionId)
            .ToArray();
        random.Shuffle(correctOnce);
        var rest = WeightedLeastRecentFirst(ShuffledOldestFirst(seen.Where(x => x.Bucket == QuestionSelectionBucket.Rest), random), random);

        return unseen
            .Concat(lastWrong)
            .Concat(correctOnce)
            .Concat(rest)
            .Take(count)
            .ToList();
    }

    private static List<QuestionAttemptSummary> ShuffledOldestFirst(IEnumerable<QuestionAttemptSummary> summaries, Random random)
    {
        var shuffled = summaries.ToArray();
        random.Shuffle(shuffled);
        return shuffled
            .OrderBy(x => x.LastAttemptedAt)
            .ToList();
    }

    // Efraimidis–Spirakis weighted sampling without replacement; weight = rank from newest (1) to oldest (n), PRD §7.2 bucket 4.
    private static List<Guid> WeightedLeastRecentFirst(IReadOnlyList<QuestionAttemptSummary> ranked, Random random)
    {
        var count = ranked.Count;
        return ranked
            .Select((summary, index) => (summary.QuestionId, Key: Math.Log(1d - random.NextDouble()) / (count - index)))
            .ToList()
            .OrderByDescending(x => x.Key)
            .Select(x => x.QuestionId)
            .ToList();
    }
}
