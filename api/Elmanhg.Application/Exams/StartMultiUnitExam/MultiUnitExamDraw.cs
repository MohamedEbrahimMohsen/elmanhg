using Elmanhg.Application.Exams.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;

namespace Elmanhg.Application.Exams.StartMultiUnitExam;

public static class MultiUnitExamDraw
{
    public static async Task<Session> StartAsync(Guid userId, MultiUnitExamUnits selection, MultiUnitExamPlan plan, int size, bool isTestMode, DateTimeOffset now, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, Random random, CancellationToken cancellationToken)
    {
        var unitIds = selection.Units
            .Select(x => x.Id)
            .ToList();
        var unitLessons = await lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var unitIdByLessonId = unitLessons.ToDictionary(x => x.Id, x => x.UnitId);
        var candidates = await questionRepository.GetServableExamCandidatesAsync(unitIds, cancellationToken).ConfigureAwait(false);
        var candidatesByUnit = selection.Units.ToDictionary(unit => unit.Id, unit => candidates
            .Where(x => unitIdByLessonId.GetValueOrDefault(x.LessonId) == unit.Id)
            .ToList());
        plan.EnsureServable(candidates.GroupBy(x => x.Type).ToDictionary(x => x.Key, x => x.Count()));
        var candidateIds = candidates
            .Select(x => x.QuestionId)
            .ToList();
        var masteries = await questionMasteryRepository.FindAsync(x => x.StudentId == userId && x.IsMastered && candidateIds.Contains(x.QuestionId), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var mastered = masteries
            .Select(x => x.QuestionId)
            .ToHashSet();
        var selectedIds = MultiUnitExamQuestionSelector.Select(plan, candidatesByUnit, mastered, random);
        var loaded = await questionRepository.FindAsync(x => selectedIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var questionsById = loaded.ToDictionary(x => x.Id);
        var questions = selectedIds
            .Where(questionsById.ContainsKey)
            .Select(x => questionsById[x])
            .ToList();
        var lessonIds = questions
            .Select(x => x.LessonId)
            .ToHashSet();
        var lessons = unitLessons
            .Where(x => lessonIds.Contains(x.Id))
            .ToList();
        return Session.StartMultiUnitExam(userId, selection.Subject.Id, selection.Units, plan, size, questions, lessons, isTestMode, now);
    }
}
