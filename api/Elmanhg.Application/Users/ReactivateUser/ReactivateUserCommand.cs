using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Users.ReactivateUser;

public sealed record ReactivateUserCommand(Guid UserId) : IRequest, IAuditableCommand
{
    public string AuditAction => "User.Reactivate";
    public string AuditResourceType => "User";
    public Guid? AuditResourceId => UserId;
}
