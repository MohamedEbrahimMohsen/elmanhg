using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.ArchiveLesson;

public sealed class ArchiveLessonHandler(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<ArchiveLessonCommand>
{
    public async Task Handle(ArchiveLessonCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var lesson = await lessonRepository.GetRequiredAsync(request.LessonId, ErrorCodes.LessonNotFound, cancellationToken).ConfigureAwait(false);

        lesson.Archive(userId);

        await lessonRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
