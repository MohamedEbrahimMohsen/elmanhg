using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Browse.RecordLessonOpening;

public sealed class RecordLessonOpeningHandler(ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<RecordLessonOpeningCommand>
{
    public async Task Handle(RecordLessonOpeningCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var lesson = await lessonRepository.GetByIdAsync(request.LessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (lesson is null || lesson.State != LessonState.Published)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        if (await lessonOpeningRepository.IsOpenedAsync(userId, lesson.Id, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var opening = LessonOpening.Record(userId, lesson, timeProvider.GetUtcNow());
        await lessonOpeningRepository.AddAsync(opening, cancellationToken).ConfigureAwait(false);

        await lessonOpeningRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
