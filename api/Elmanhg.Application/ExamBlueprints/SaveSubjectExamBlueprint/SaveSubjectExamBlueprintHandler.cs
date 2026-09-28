using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.SaveSubjectExamBlueprint;

public sealed class SaveSubjectExamBlueprintHandler(ISubjectRepository subjectRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<SaveSubjectExamBlueprintCommand, ExamBlueprintResult>
{
    public async Task<ExamBlueprintResult> Handle(SaveSubjectExamBlueprintCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var servable = ServableTypeCounts.ForSubject(await questionRepository.CountServableByUnitAndTypeAsync(subject.Id, cancellationToken).ConfigureAwait(false));
        var shape = ExamBlueprintShapeGenerator.Generate(request.Blueprint);
        var blueprint = await examBlueprintRepository.FirstOrDefaultAsync(x => x.SubjectId == subject.Id && x.UnitId == null, cancellationToken).ConfigureAwait(false);
        if (blueprint is null)
        {
            blueprint = ExamBlueprint.CreateForSubject(subject, shape, servable, userId);
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
