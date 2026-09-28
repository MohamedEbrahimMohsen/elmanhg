using Core.Localization;
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
    public static async Task<ExamSessionResult> LoadAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, decimal correctThreshold, int weakestObjectiveCount, DateTimeOffset now, ILocalizer localizer, CancellationToken cancellationToken)
    {
        var unitId = UnitExamScope.FromJson(session.Scope).UnitId;
        var unit = await unitRepository.GetByIdAsync(unitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var subject = unit is null ? null : await subjectRepository.GetByIdAsync(unit.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        List<ExamUnitResult> units = [new ExamUnitResult(unitId, unit?.Name)];
        if (!session.IsSubmitted)
        {
            return ExamSessionResultGenerator.Generate(session, revisions, subject?.Name, units, [], [], now, localizer);
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
        var lessonResults = ExamBreakdownResultGenerator.Lessons(ExamBreakdown.ByLesson(session, placements, correctThreshold), lessons);
        var objectiveResults = ExamBreakdownResultGenerator.Objectives(ExamBreakdown.WeakestObjectives(session, placements, correctThreshold, weakestObjectiveCount), lessons);
        return ExamSessionResultGenerator.Generate(session, revisions, subject?.Name, units, lessonResults, objectiveResults, now, localizer);
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
