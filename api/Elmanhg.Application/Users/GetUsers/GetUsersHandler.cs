using Core.DDD.Models;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Users.GetUsers;

public sealed class GetUsersHandler(IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<GetUsersQuery, PageData<UserSummaryResult>>
{
    public async Task<PageData<UserSummaryResult>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var actorId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var page = await userRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetUsersFilter.Build(request), orderBy: query => query.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var ids = page.Items
            .Select(x => x.Id)
            .ToList();
        var activeAdminCount = request.Role == UserRole.Admin ? await userRepository.CountAsync(cancellationToken, x => x.Role == UserRole.Admin && x.Status == UserStatus.Active).ConfigureAwait(false) : 0;
        var entitlements = request.Role == UserRole.Student && ids.Count > 0 ? await LoadEntitlementsAsync(ids, cancellationToken).ConfigureAwait(false) : [];
        var subjectsByTeacher = request.Role == UserRole.Teacher && ids.Count > 0 ? await LoadSubjectIdsAsync(ids, cancellationToken).ConfigureAwait(false) : [];

        return page.Map(x => UserSummaryResultGenerator.Generate(x, actorId, activeAdminCount, entitlements.GetValueOrDefault(x.Id), subjectsByTeacher.GetValueOrDefault(x.Id) ?? []));
    }

    private async Task<Dictionary<Guid, StudentEntitlement>> LoadEntitlementsAsync(List<Guid> studentIds, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var grace = subscriptionsOptions.Value.GracePeriod;
        var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledForStudents(studentIds, now, grace), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return subscriptions
            .GroupBy(x => x.StudentId)
            .ToDictionary(x => x.Key, x => StudentEntitlement.Resolve(x, now, grace));
    }

    private async Task<Dictionary<Guid, List<Guid>>> LoadSubjectIdsAsync(List<Guid> teacherIds, CancellationToken cancellationToken)
    {
        var assignments = await teacherSubjectRepository.FindAsync(x => teacherIds.Contains(x.TeacherId), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return assignments
            .GroupBy(x => x.TeacherId)
            .ToDictionary(x => x.Key, x => x.Select(assignment => assignment.SubjectId).ToList());
    }
}
