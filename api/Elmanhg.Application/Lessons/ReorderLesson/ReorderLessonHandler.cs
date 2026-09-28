using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.ReorderLesson;

public sealed class ReorderLessonHandler(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<ReorderLessonCommand>
{
    public async Task Handle(ReorderLessonCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var lesson = await lessonRepository.GetByIdAsync(request.LessonId, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var siblings = await lessonRepository.FindAsync(x => x.UnitId == lesson.UnitId, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate)).ConfigureAwait(false);
        siblings.RemoveAll(x => x.Id == lesson.Id);
        siblings.Insert(Math.Min(request.Position, siblings.Count + 1) - 1, lesson);
        for (var index = 0; index < siblings.Count; index++)
        {
            siblings[index].MoveTo(index + 1, currentUserService.UserId.Value);
        }

        await lessonRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
