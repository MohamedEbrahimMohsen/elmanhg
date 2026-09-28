using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Mastery.GetMasteryOverview;

public sealed class GetMasteryOverviewHandler(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IOptions<ProgressOptions> progressOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMasteryOverviewQuery, MasteryOverviewResult>
{
    public async Task<MasteryOverviewResult> Handle(GetMasteryOverviewQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var lessons = await questionMasteryRepository.GetLessonCountsAsync(userId, null, cancellationToken).ConfigureAwait(false);
        var subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false) ?? [];

        var options = progressOptions.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.StreakTimeZone);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var activeDays = await sessionRepository.GetQuizActivityDaysAsync(userId, options.StreakTimeZone, now.AddDays(-options.StreakMaxDays), cancellationToken).ConfigureAwait(false);

        var next = NextLessonRecommendation.Pick(lessons);
        var nextLesson = next is null ? null : await lessonRepository.GetByIdAsync(next.LessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return MasteryOverviewResultGenerator.Generate(lessons, subjects, StudyStreak.Count(activeDays, today), next, nextLesson);
    }
}
