using Core.DDD.Models;
using Elmanhg.Application.Progress.GetSessionHistory;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Progress.Shared;

public static class SessionHistoryLoader
{
    public static async Task<PageData<SessionHistoryItemResult>> LoadAsync(ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, Guid studentId, SessionHistoryKind? kind, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var page = await sessionRepository.FindPaginatedAsync(pageNumber, pageSize, cancellationToken, filter: GetSessionHistoryFilter.Build(studentId, kind), orderBy: query => query.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var lessonIds = page.Items
            .Where(x => x.Kind == SessionKind.Quiz)
            .Select(x => QuizScope.FromJson(x.Scope).LessonId)
            .Distinct()
            .ToList();
        var unitIds = page.Items
            .Where(x => x.IsExam)
            .SelectMany(x => x.GetExamUnitIds())
            .Distinct()
            .ToList();
        List<Lesson> lessons = lessonIds.Count == 0 ? [] : await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        List<CurriculumUnit> units = unitIds.Count == 0 ? [] : await unitRepository.FindAsync(x => unitIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var bestByScopeKey = page.Items.Any(ExamBestScoreSpecification.IsSatisfiedBy) ? (await sessionRepository.GetBestExamScoresAsync(studentId, cancellationToken).ConfigureAwait(false)).ToDictionary(x => x.ScopeKey, x => x.BestScorePercent) : new Dictionary<string, decimal>();

        return new PageData<SessionHistoryItemResult>
        {
            Items = page.Items
                .Select(x => SessionHistoryResultGenerator.Generate(x, lessons, units, bestByScopeKey))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
