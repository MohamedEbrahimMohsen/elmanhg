using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;

public sealed class DeleteExamBlueprintHandler(IExamBlueprintRepository examBlueprintRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteExamBlueprintCommand>
{
    public async Task Handle(DeleteExamBlueprintCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var blueprint = await examBlueprintRepository.GetByIdAsync(request.ExamBlueprintId, cancellationToken).ConfigureAwait(false);
        if (blueprint is null)
        {
            throw new NotFoundCoreException(ErrorCodes.ExamBlueprintNotFound);
        }

        blueprint.Delete(currentUserService.UserId.Value);

        await examBlueprintRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
