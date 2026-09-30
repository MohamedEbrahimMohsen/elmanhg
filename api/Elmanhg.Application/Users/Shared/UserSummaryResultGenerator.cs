using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Users.Shared;

public static class UserSummaryResultGenerator
{
    public static UserSummaryResult Generate(User user, Guid actorId, int activeAdminCount, StudentEntitlement? entitlement, List<Guid> subjectIds)
    {
        var isStudent = user.Role == UserRole.Student;
        return new UserSummaryResult(user.Id, user.DisplayName, user.Role, user.Status, ContactMask.MaskPhone(user.PhoneNumber), ContactMask.MaskEmail(user.Email), user.IsInvitationPending, user.CanBeSuspendedBy(actorId, activeAdminCount), user.CreationDate, isStudent ? (entitlement ?? StudentEntitlement.Free).Tier : null, isStudent && (entitlement?.HasAskTeacher ?? false), subjectIds);
    }
}
