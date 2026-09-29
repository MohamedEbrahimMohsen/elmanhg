using Elmanhg.Application.Shared.AiService;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AvatarMessageContext(AiContextBundle Bundle, IReadOnlyList<AiChatSource> Sources);
