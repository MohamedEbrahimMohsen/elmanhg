using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var thread = await teacherThreadRepository.GetRequiredAsync(x => x.Id == request.ThreadId && x.StudentId == userId, ErrorCodes.TeacherThreadNotFound, cancellationToken, include: query => query.Include(x => x.Messages), asNoTracking: true).ConfigureAwait(false);
        return TeacherThreadResultGenerator.Generate(thread, timeProvider.GetUtcNow());
    }
}
