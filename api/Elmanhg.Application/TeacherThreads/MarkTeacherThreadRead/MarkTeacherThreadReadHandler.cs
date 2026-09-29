using Core.Errors;
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
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherThreadNotFound);
        thread.MarkRepliesRead(timeProvider.GetUtcNow());

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
