using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherInbox.GetInboxThread;

public sealed class GetInboxThreadHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherSubjectRepository teacherSubjectRepository, IUserRepository userRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetInboxThreadQuery, TeacherInboxThreadResult>
{
    public async Task<TeacherInboxThreadResult> Handle(GetInboxThreadQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var thread = await teacherThreadRepository.GetRequiredAsync(x => x.Id == request.ThreadId, ErrorCodes.TeacherThreadNotFound, cancellationToken, include: query => query.Include(x => x.Messages), asNoTracking: true).ConfigureAwait(false);
        await TeacherInboxAccess.EnsureCanAccessAsync(thread.SubjectId, userId, currentUserService.GetClaim(ClaimTypes.Role), teacherSubjectRepository, cancellationToken).ConfigureAwait(false);

        var names = await TeacherInboxNames.LoadAsync(userRepository, [thread], cancellationToken).ConfigureAwait(false);
        return TeacherInboxResultGenerator.GenerateThread(thread, names, userId, timeProvider.GetUtcNow());
    }
}
