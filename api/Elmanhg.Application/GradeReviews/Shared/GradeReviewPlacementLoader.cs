using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.GradeReviews.Shared;

public static class GradeReviewPlacementLoader
{
    public static async Task<(Dictionary<Guid, Question> Questions, Dictionary<Guid, GradeReviewPlacement> Placements)> LoadAsync(IReadOnlyCollection<Guid> questionIds, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, CancellationToken cancellationToken)
    {
        var questions = (await questionRepository.FindAsync(x => questionIds.Contains(x.Id), cancellationToken, include: query => query.IgnoreQueryFilters(), asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var lessonIds = questions.Values
            .Select(x => x.LessonId)
            .Distinct()
            .ToList();
        var lessons = (await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var unitIds = lessons.Values
            .Select(x => x.UnitId)
            .Distinct()
            .ToList();
        var units = (await unitRepository.FindAsync(x => unitIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var placements = questions.Values.ToDictionary(x => x.Id, x => Placement(x, lessons, units));
        return (questions, placements);
    }

    private static GradeReviewPlacement Placement(Question question, Dictionary<Guid, Lesson> lessons, Dictionary<Guid, CurriculumUnit> units)
    {
        var lesson = lessons.GetValueOrDefault(question.LessonId);
        var unit = lesson is null ? null : units.GetValueOrDefault(lesson.UnitId);
        return lesson is null || unit is null ? GradeReviewPlacement.Unknown : new GradeReviewPlacement(unit.Name, lesson.Name);
    }
}
