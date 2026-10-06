using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.SaveUnitExamBlueprint;

public sealed class SaveUnitExamBlueprintHandler(ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<SaveUnitExamBlueprintCommand, ExamBlueprintResult>
{
    public async Task<ExamBlueprintResult> Handle(SaveUnitExamBlueprintCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var unit = await unitRepository.GetRequiredAsync(request.UnitId, ErrorCodes.UnitNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var servable = ServableTypeCounts.ForUnit(await questionRepository.CountServableByUnitAndTypeAsync(unit.SubjectId, cancellationToken).ConfigureAwait(false), unit.Id);
        var shape = ExamBlueprintShapeGenerator.Generate(request.Blueprint);
        var blueprint = await examBlueprintRepository.FirstOrDefaultAsync(x => x.UnitId == unit.Id, cancellationToken).ConfigureAwait(false);
        if (blueprint is null)
        {
            blueprint = ExamBlueprint.CreateForUnit(unit, shape, servable, userId);
            await examBlueprintRepository.AddAsync(blueprint, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            blueprint.Update(shape, servable, userId);
        }

        await examBlueprintRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ExamBlueprintResultGenerator.Generate(blueprint);
    }
}
