using Core.Localization;
using Core.Storage;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamSessionResultLoader
{
    public static async Task<ExamSessionResult> LoadAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, decimal correctThreshold, int weakestObjectiveCount, DateTimeOffset now, ILocalizer localizer, IFileStorage fileStorage, CancellationToken cancellationToken)
    {
        var unitIds = session.GetExamUnitIds();
        List<CurriculumUnit> units = [];
        foreach (var id in unitIds)
        {
            var unit = await unitRepository.GetByIdAsync(id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            if (unit is not null)
            {
                units.Add(unit);
            }
        }

        Guid? subjectId = session.Kind == SessionKind.MultiUnitExam ? MultiUnitExamScope.FromJson(session.Scope).SubjectId : units.FirstOrDefault()?.SubjectId;
        var subject = subjectId is null ? null : await subjectRepository.GetByIdAsync(subjectId.Value, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var unitResults = unitIds
            .Select(id => new ExamUnitResult(id, units.FirstOrDefault(x => x.Id == id)?.Name))
            .ToList();
        if (!session.IsSubmitted)
        {
            return ExamSessionResultGenerator.Generate(session, revisions, subjectId, subject?.Name, unitResults, [], [], [], now, localizer, fileStorage);
        }

        var questionIds = session.Items
            .Select(x => x.QuestionId)
            .ToList();
        var questions = await questionRepository.FindAsync(x => questionIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var lessonIds = questions
            .Select(x => x.LessonId)
            .Distinct()
            .ToList();
        var lessons = await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, include: query => query.Include(x => x.Objectives), asNoTracking: true).ConfigureAwait(false);
        var placements = Place(questions, lessons);
        var lessonShares = ExamBreakdown.ByLesson(session, placements, correctThreshold);
        var unitShares = ExamBreakdown.ByUnit(lessonShares, lessons.ToDictionary(x => x.Id, x => x.UnitId), unitIds);
        var lessonResults = ExamBreakdownResultGenerator.Lessons(lessonShares, lessons);
        var unitBreakdown = ExamBreakdownResultGenerator.Units(unitShares, units);
        var objectiveResults = ExamBreakdownResultGenerator.Objectives(ExamBreakdown.WeakestObjectives(session, placements, correctThreshold, weakestObjectiveCount), lessons);
        return ExamSessionResultGenerator.Generate(session, revisions, subjectId, subject?.Name, unitResults, lessonResults, unitBreakdown, objectiveResults, now, localizer, fileStorage);
    }

    private static List<ExamItemPlacement> Place(IEnumerable<Question> questions, IReadOnlyCollection<Lesson> lessons)
    {
        return questions
            .SelectMany(question => lessons
                .Where(x => x.Id == question.LessonId)
                .Take(1)
                .Select(lesson => Placement(question, lesson)))
            .ToList();
    }

    private static ExamItemPlacement Placement(Question question, Lesson lesson)
    {
        var objective = lesson.Objectives.FirstOrDefault(x => x.Id == question.ObjectiveId);
        return new ExamItemPlacement(question.Id, lesson.Id, lesson.Order, objective?.Id, objective?.Order ?? 0);
    }
}
