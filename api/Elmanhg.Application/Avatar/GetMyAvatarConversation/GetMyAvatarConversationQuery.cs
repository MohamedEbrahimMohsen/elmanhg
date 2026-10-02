using Elmanhg.Application.Avatar.Shared;
using MediatR;

namespace Elmanhg.Application.Avatar.GetMyAvatarConversation;

public sealed record GetMyAvatarConversationQuery(Guid ConversationId) : IRequest<StudentAvatarConversationDetailResult>;
