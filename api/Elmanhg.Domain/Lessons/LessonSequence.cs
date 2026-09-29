using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.Lessons;

public static class LessonSequence
{
    // D6: curriculum order is unit order then lesson order; ties fall back to creation date then id so the sequence is stable.
    public static List<Lesson> Order(IEnumerable<CurriculumUnit> units, IEnumerable<Lesson> lessons)
    {
        var rank = units
            .OrderBy(x => x.Order)
            .ThenBy(x => x.CreationDate)
            .ThenBy(x => x.Id)
            .Select((unit, index) => (unit.Id, index))
            .ToDictionary(x => x.Id, x => x.index);
        return lessons
            .Where(x => rank.ContainsKey(x.UnitId))
            .OrderBy(x => rank[x.UnitId])
            .ThenBy(x => x.Order)
            .ThenBy(x => x.CreationDate)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public static LessonNeighbours Neighbours(IReadOnlyList<Lesson> ordered, Guid lessonId)
    {
        var index = ordered
            .Select((lesson, position) => (lesson.Id, position))
            .FirstOrDefault(x => x.Id == lessonId, (Id: Guid.Empty, position: -1)).position;
        if (index < 0)
        {
            return new LessonNeighbours(null, null);
        }

        return new LessonNeighbours(index > 0 ? ordered[index - 1] : null, index < ordered.Count - 1 ? ordered[index + 1] : null);
    }
}

public sealed record LessonNeighbours(Lesson? Previous, Lesson? Next);
