using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherInbox.ClaimTeacherThread;

public sealed class ClaimTeacherThreadHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherSubjectRepository teacherSubjectRepository, IUserRepository userRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<ClaimTeacherThreadCommand, TeacherInboxThreadResult>
{
    public async Task<TeacherInboxThreadResult> Handle(ClaimTeacherThreadCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherThreadNotFound);
        await TeacherInboxAccess.EnsureCanAccessAsync(thread.SubjectId, userId, currentUserService.GetClaim(ClaimTypes.Role), teacherSubjectRepository, cancellationToken).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        thread.Claim(userId, now);

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var names = await TeacherInboxNames.LoadAsync(userRepository, [thread], cancellationToken).ConfigureAwait(false);
        return TeacherInboxResultGenerator.GenerateThread(thread, names, userId, now);
    }
}
