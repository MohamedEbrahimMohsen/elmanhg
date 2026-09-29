namespace Elmanhg.Domain.Sessions.Exams;

public static class ExamBreakdown
{
    public static List<ExamShare> ByLesson(Session session, IReadOnlyCollection<ExamItemPlacement> placements, decimal correctThreshold)
    {
        if (!session.IsSubmitted)
        {
            return [];
        }

        return Rows(session, placements)
            .GroupBy(x => x.Placement.LessonId)
            .Select(x => Share(x.Key, 0, x.ToList(), correctThreshold))
            .OrderBy(x => x.LessonOrder)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public static List<ExamShare> WeakestObjectives(Session session, IReadOnlyCollection<ExamItemPlacement> placements, decimal correctThreshold, int count)
    {
        if (!session.IsSubmitted)
        {
            return [];
        }

        return Rows(session, placements)
            .Where(x => x.Placement.ObjectiveId is not null)
            .GroupBy(x => x.Placement.ObjectiveId.GetValueOrDefault())
            .Select(x => Share(x.Key, x.First().Placement.ObjectiveOrder, x.ToList(), correctThreshold))
            .Where(x => x.Score < x.MaxScore)
            .OrderBy(x => x.ScorePercent)
            .ThenBy(x => x.LessonOrder)
            .ThenBy(x => x.ObjectiveOrder)
            .ThenBy(x => x.Id)
            .Take(count)
            .ToList();
    }

    public static List<ExamUnitShare> ByUnit(IEnumerable<ExamShare> lessonShares, IReadOnlyDictionary<Guid, Guid> unitIdByLessonId, IReadOnlyList<Guid> unitOrder)
    {
        return lessonShares
            .Where(x => unitIdByLessonId.ContainsKey(x.LessonId))
            .GroupBy(x => unitIdByLessonId[x.LessonId])
            .Select(x => new ExamUnitShare(x.Key, x.Sum(share => share.QuestionCount), x.Sum(share => share.CorrectCount), x.Sum(share => share.Score), x.Sum(share => share.MaxScore)))
            .OrderBy(x => UnitPosition(unitOrder, x.UnitId))
            .ThenBy(x => x.UnitId)
            .ToList();
    }

    private static int UnitPosition(IReadOnlyList<Guid> unitOrder, Guid unitId)
    {
        for (var index = 0; index < unitOrder.Count; index++)
        {
            if (unitOrder[index] == unitId)
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static List<Row> Rows(Session session, IReadOnlyCollection<ExamItemPlacement> placements)
    {
        var placementsByQuestion = placements
            .DistinctBy(x => x.QuestionId)
            .ToDictionary(x => x.QuestionId);
        return session.Items
            .Where(x => placementsByQuestion.ContainsKey(x.QuestionId))
            .Select(x => new Row(placementsByQuestion[x.QuestionId], x, session.FindAttempt(x.QuestionId)))
            .ToList();
    }

    private static ExamShare Share(Guid id, int objectiveOrder, List<Row> rows, decimal correctThreshold)
    {
        var placement = rows[0].Placement;
        var correct = rows.Count(x => x.Attempt is not null && x.Attempt.NormalisedScore >= correctThreshold);
        return new ExamShare(id, placement.LessonId, placement.LessonOrder, objectiveOrder, rows.Count, correct, rows.Sum(x => x.Attempt?.Score ?? 0m), rows.Sum(x => x.Item.MaxScore));
    }

    private sealed record Row(ExamItemPlacement Placement, SessionItem Item, Attempt? Attempt);
}
