namespace Elmanhg.Application.Shared.Messaging;

public abstract record OutboundMessage(Guid RecipientUserId, string Address);
