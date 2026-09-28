using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Progress.GetSessionHistory;

public sealed class GetSessionHistoryHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSessionHistoryQuery, PageData<SessionHistoryItemResult>>
{
    public async Task<PageData<SessionHistoryItemResult>> Handle(GetSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var page = await sessionRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetSessionHistoryFilter.Build(userId, request.Kind), orderBy: query => query.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var lessonIds = page.Items
            .Where(x => x.Kind == SessionKind.Quiz)
            .Select(x => QuizScope.FromJson(x.Scope).LessonId)
            .Distinct()
            .ToList();
        var unitIds = page.Items
            .Where(x => x.Kind == SessionKind.UnitExam)
            .Select(x => UnitExamScope.FromJson(x.Scope).UnitId)
            .Distinct()
            .ToList();
        List<Lesson> lessons = lessonIds.Count == 0 ? [] : await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        List<CurriculumUnit> units = unitIds.Count == 0 ? [] : await unitRepository.FindAsync(x => unitIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return new PageData<SessionHistoryItemResult>
        {
            Items = page.Items
                .Select(x => SessionHistoryResultGenerator.Generate(x, lessons, units))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
