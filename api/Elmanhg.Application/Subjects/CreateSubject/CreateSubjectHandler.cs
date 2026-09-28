using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;
using MediatR;

namespace Elmanhg.Application.Subjects.CreateSubject;

public sealed class CreateSubjectHandler(ISubjectRepository subjectRepository, ICurrentUserService currentUserService) : IRequestHandler<CreateSubjectCommand, CreateSubjectResult>
{
    public async Task<CreateSubjectResult> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var last = await subjectRepository.FirstOrDefaultAsync(_ => true, cancellationToken, orderBy: query => query.OrderByDescending(x => x.Order), asNoTracking: true).ConfigureAwait(false);
        var subject = Subject.Create(request.Name, (last?.Order ?? 0) + 1, currentUserService.UserId.Value);

        await subjectRepository.AddAsync(subject, cancellationToken).ConfigureAwait(false);
        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateSubjectResult(subject.Id);
    }
}
