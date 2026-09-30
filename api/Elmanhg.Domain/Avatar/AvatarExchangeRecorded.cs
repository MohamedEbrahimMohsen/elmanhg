using Core.DDD.Entities;

namespace Elmanhg.Domain.Avatar;

public sealed record AvatarExchangeRecorded(AvatarConversation Conversation, AvatarMessage StudentMessage, AvatarMessage AssistantMessage) : DomainEvent;
