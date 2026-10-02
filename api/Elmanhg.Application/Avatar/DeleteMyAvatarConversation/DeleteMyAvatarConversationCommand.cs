using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Avatar.DeleteMyAvatarConversation;

public sealed record DeleteMyAvatarConversationCommand(Guid ConversationId) : IRequest, IAuditableCommand
{
    public string AuditAction => "AvatarConversation.Delete";
    public string AuditResourceType => "AvatarConversation";
    public Guid? AuditResourceId => ConversationId;
}
