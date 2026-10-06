using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.TeacherThreads.MarkTeacherThreadRead;

public sealed class MarkTeacherThreadReadHandler(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<MarkTeacherThreadReadCommand>
{
    public async Task Handle(MarkTeacherThreadReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var thread = await teacherThreadRepository.GetRequiredAsync(x => x.Id == request.ThreadId && x.StudentId == userId, ErrorCodes.TeacherThreadNotFound, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false);
        thread.MarkRepliesRead(timeProvider.GetUtcNow());

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
