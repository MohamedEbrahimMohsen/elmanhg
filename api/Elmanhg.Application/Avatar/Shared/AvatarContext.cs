using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Shared.AiService;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AvatarContext(AiContextBundle Bundle, Guid? LessonId, IReadOnlyList<LessonContentMatchResult> Matches);
