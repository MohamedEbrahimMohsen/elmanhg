using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Identity;

public partial class User
{
    public bool IsInvitationPending => Role != UserRole.Student && PasswordHash is null;

    public bool CanBeSuspendedBy(Guid actorId, int activeAdminCount) => Id != actorId && IsActive && !(Role == UserRole.Admin && activeAdminCount <= 1);

    public void Suspend(Guid suspendedBy, int activeAdminCount)
    {
        if (Id == suspendedBy)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.UserCannotSuspendSelf);
        }

        if (Role == UserRole.Admin && IsActive && activeAdminCount <= 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LastActiveAdmin);
        }

        Suspend();
        UpdatedBy = suspendedBy;
    }

    public void Reactivate(Guid reactivatedBy)
    {
        if (Status == UserStatus.Active)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.UserNotSuspended);
        }

        Status = UserStatus.Active;
        UpdatedBy = reactivatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void SetContactPhoneNumber(string? phoneNumber, Guid updatedBy)
    {
        if (Role != UserRole.Teacher)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PhoneNumberTeachersOnly);
        }

        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        PhoneNumberConfirmed = false;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
