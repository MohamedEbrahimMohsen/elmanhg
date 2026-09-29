using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.TeacherThreads.GetMyTeacherThread;

public sealed class GetMyTeacherThreadHandler(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMyTeacherThreadQuery, TeacherThreadResult>
{
    public async Task<TeacherThreadResult> Handle(GetMyTeacherThreadQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Messages), asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherThreadNotFound);
        return TeacherThreadResultGenerator.Generate(thread, timeProvider.GetUtcNow());
    }
}
