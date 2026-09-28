using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Progress.GetWeakSpots;

public sealed class GetWeakSpotsHandler(IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository, IOptions<ProgressOptions> progressOptions, ICurrentUserService currentUserService) : IRequestHandler<GetWeakSpotsQuery, WeakSpotsResult>
{
    public async Task<WeakSpotsResult> Handle(GetWeakSpotsQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var options = progressOptions.Value;
        var lessonCounts = await questionMasteryRepository.GetLessonCountsAsync(userId, null, cancellationToken).ConfigureAwait(false);
        var objectiveCounts = await questionMasteryRepository.GetObjectiveCountsAsync(userId, cancellationToken).ConfigureAwait(false);
        var weakLessons = WeakSpots.PickLessons(lessonCounts, options.WeakLessonCount);
        var weakObjectives = WeakSpots.PickObjectives(objectiveCounts, options.WeakObjectiveCount);
        var lessonIds = weakLessons
            .Select(x => x.LessonId)
            .Union(weakObjectives.Select(x => x.LessonId))
            .ToList();
        if (lessonIds.Count == 0)
        {
            return new WeakSpotsResult([], []);
        }

        var lessons = await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, include: query => query.Include(x => x.Objectives), asNoTracking: true).ConfigureAwait(false);
        var subjectIds = weakLessons
            .Select(x => x.SubjectId)
            .Union(weakObjectives.Select(x => x.SubjectId))
            .ToList();
        var subjects = await subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return WeakSpotsResultGenerator.Generate(weakLessons, weakObjectives, lessons, subjects);
    }
}
