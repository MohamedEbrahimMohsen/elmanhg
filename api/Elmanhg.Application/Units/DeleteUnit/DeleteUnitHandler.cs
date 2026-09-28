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
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken).ConfigureAwait(false);
        if (unit is null || unit.SubjectId != request.SubjectId)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var hasLessons = await lessonRepository.AnyInUnitAsync(unit.Id, cancellationToken).ConfigureAwait(false);
        unit.Delete(hasLessons, currentUserService.UserId.Value);

        await unitRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
