using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Units.ReorderUnit;

public sealed class ReorderUnitHandler(ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<ReorderUnitCommand>
{
    public async Task Handle(ReorderUnitCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var siblings = await unitRepository.FindAsync(x => x.SubjectId == request.SubjectId, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate)).ConfigureAwait(false);
        var unit = siblings.FirstOrDefault(x => x.Id == request.UnitId);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        siblings.Remove(unit);
        siblings.Insert(Math.Min(request.Position, siblings.Count + 1) - 1, unit);
        for (var index = 0; index < siblings.Count; index++)
        {
            siblings[index].MoveTo(index + 1, currentUserService.UserId.Value);
        }

        await unitRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
