using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.UnpublishLesson;

public sealed class UnpublishLessonHandler(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<UnpublishLessonCommand>
{
    public async Task Handle(UnpublishLessonCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var lesson = await lessonRepository.GetRequiredAsync(request.LessonId, ErrorCodes.LessonNotFound, cancellationToken).ConfigureAwait(false);

        lesson.Unpublish(userId);

        await lessonRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
