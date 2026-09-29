using Elmanhg.Application.Avatar.Shared;
using MediatR;

namespace Elmanhg.Application.Avatar.GetAvatarConversation;

public sealed record GetAvatarConversationQuery(Guid ConversationId) : IRequest<AdminAvatarConversationDetailResult>;
