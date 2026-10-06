using Core.DDD.Models;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherInbox.GetTeacherInbox;

public sealed class GetTeacherInboxHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherSubjectRepository teacherSubjectRepository, IUserRepository userRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetTeacherInboxQuery, PageData<TeacherInboxItemResult>>
{
    public async Task<PageData<TeacherInboxItemResult>> Handle(GetTeacherInboxQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var isAdmin = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
        List<Guid>? subjectIds = null;
        if (!isAdmin)
        {
            var assignments = await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            subjectIds = assignments
                .Select(x => x.SubjectId)
                .ToList();
        }

        var page = await teacherThreadRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: TeacherInboxQueryShape.Filter(subjectIds, request.Filter, userId), include: query => query.Include(x => x.Messages), orderBy: TeacherInboxQueryShape.Order, asNoTracking: true).ConfigureAwait(false);
        var names = await TeacherInboxNames.LoadAsync(userRepository, page.Items, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        return page.Map(x => TeacherInboxResultGenerator.GenerateItem(x, names, userId, now));
    }
}
