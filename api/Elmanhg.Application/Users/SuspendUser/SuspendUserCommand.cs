using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Users.SuspendUser;

public sealed record SuspendUserCommand(Guid UserId) : IRequest, IAuditableCommand
{
    public string AuditAction => "User.Suspend";
    public string AuditResourceType => "User";
    public Guid? AuditResourceId => UserId;
}
