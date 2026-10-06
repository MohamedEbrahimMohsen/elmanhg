using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;

public sealed class DeleteExamBlueprintHandler(IExamBlueprintRepository examBlueprintRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteExamBlueprintCommand>
{
    public async Task Handle(DeleteExamBlueprintCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var blueprint = await examBlueprintRepository.GetRequiredAsync(request.ExamBlueprintId, ErrorCodes.ExamBlueprintNotFound, cancellationToken).ConfigureAwait(false);

        blueprint.Delete(userId);

        await examBlueprintRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
