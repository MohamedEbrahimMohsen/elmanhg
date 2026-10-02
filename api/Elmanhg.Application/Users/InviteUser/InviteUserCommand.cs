using Core.Auditing;
using Elmanhg.Domain.Identity;
using MediatR;

namespace Elmanhg.Application.Users.InviteUser;

public sealed record InviteUserCommand(UserRole Role, string DisplayName, string Email, string? PhoneNumber = null) : IRequest<InviteUserResult>, IAuditableCommand
{
    public string AuditAction => "User.Invite";
    public string AuditResourceType => "User";
    public Guid? AuditResourceId => null;
}
