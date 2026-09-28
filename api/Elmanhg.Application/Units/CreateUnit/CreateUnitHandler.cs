using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Units.CreateUnit;

public sealed class CreateUnitHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<CreateUnitCommand, CreateUnitResult>
{
    public async Task<CreateUnitResult> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var last = await unitRepository.FirstOrDefaultAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderByDescending(x => x.Order), asNoTracking: true).ConfigureAwait(false);
        var unit = CurriculumUnit.Create(subject, request.Name, (last?.Order ?? 0) + 1, currentUserService.UserId.Value);

        await unitRepository.AddAsync(unit, cancellationToken).ConfigureAwait(false);
        await unitRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateUnitResult(unit.Id);
    }
}
