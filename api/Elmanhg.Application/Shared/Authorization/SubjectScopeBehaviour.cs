using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Teachers;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.Shared.Authorization;

public sealed class SubjectScopeBehaviour<TRequest, TResponse>(ICurrentUserService currentUserService, ITeacherSubjectRepository teacherSubjectRepository) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ISubjectScopedRequest scoped)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var role = currentUserService.GetClaim(ClaimTypes.Role);
        if (role is nameof(UserRole.Student) or nameof(UserRole.Admin))
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (currentUserService.UserId is null)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        if (!await teacherSubjectRepository.IsAssignedAsync(currentUserService.UserId.Value, scoped.SubjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
