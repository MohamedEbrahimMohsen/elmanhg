using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Units.UpdateUnit;

public sealed class UpdateUnitHandler(ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<UpdateUnitCommand>
{
    public async Task Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
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

        unit.Rename(request.Name, currentUserService.UserId.Value);

        await unitRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
