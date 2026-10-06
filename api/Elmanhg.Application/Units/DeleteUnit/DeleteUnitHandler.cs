using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Units.DeleteUnit;

public sealed class DeleteUnitHandler(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteUnitCommand>
{
    public async Task Handle(DeleteUnitCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken).ConfigureAwait(false);
        if (unit is null || unit.SubjectId != request.SubjectId)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var hasLessons = await lessonRepository.AnyInUnitAsync(unit.Id, cancellationToken).ConfigureAwait(false);
        unit.Delete(hasLessons, userId);

        await unitRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
