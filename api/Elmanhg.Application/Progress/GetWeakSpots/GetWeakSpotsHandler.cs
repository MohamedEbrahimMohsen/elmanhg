using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Progress.GetWeakSpots;

public sealed class GetWeakSpotsHandler(IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository, IOptions<ProgressOptions> progressOptions, ICurrentUserService currentUserService) : IRequestHandler<GetWeakSpotsQuery, WeakSpotsResult>
{
    public async Task<WeakSpotsResult> Handle(GetWeakSpotsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        return await WeakSpotsLoader.LoadAsync(questionMasteryRepository, lessonRepository, subjectRepository, progressOptions.Value, userId, cancellationToken).ConfigureAwait(false);
    }
}
