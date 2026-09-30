using Core.Auditing;

namespace Elmanhg.Application.Users.InviteUser;

public sealed record InviteUserResult(Guid UserId, bool EmailSent) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => UserId;
}
