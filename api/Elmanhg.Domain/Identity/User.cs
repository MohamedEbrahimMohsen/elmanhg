using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Domain.Identity;

public class User : IdentityUser<Guid>, IAuditEntity
{
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdationDate { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public string DisplayName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public bool IsActive => Status == UserStatus.Active;

    public static User CreateStudentWithPhone(string displayName, string phoneNumber)
    {
        var user = Create(displayName, UserRole.Student, userName: phoneNumber);
        user.PhoneNumber = phoneNumber;
        user.PhoneNumberConfirmed = true;
        return user;
    }

    public static User CreateStudentWithEmail(string displayName, string email)
    {
        var user = Create(displayName, UserRole.Student, userName: email);
        user.Email = email;
        user.EmailConfirmed = false;
        return user;
    }

    public static User CreateAdmin(string displayName, string email)
    {
        var user = Create(displayName, UserRole.Admin, userName: email);
        user.Email = email;
        user.EmailConfirmed = true;
        return user;
    }

    public void Suspend()
    {
        if (Status == UserStatus.Suspended)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.UserAlreadySuspended);
        }

        Status = UserStatus.Suspended;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    private static User Create(string displayName, UserRole role, string userName)
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            DisplayName = displayName,
            Role = role,
            Status = UserStatus.Active,
            CreationDate = now,
            UpdationDate = now,
        };
    }
}
