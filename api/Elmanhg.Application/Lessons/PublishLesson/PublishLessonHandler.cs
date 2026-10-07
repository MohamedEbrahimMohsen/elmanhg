using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.PublishLesson;

public sealed class PublishLessonHandler(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<PublishLessonCommand>
{
    public async Task Handle(PublishLessonCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var lesson = await lessonRepository.GetRequiredAsync(request.LessonId, ErrorCodes.LessonNotFound, cancellationToken).ConfigureAwait(false);

        lesson.Publish(userId);

        await lessonRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
