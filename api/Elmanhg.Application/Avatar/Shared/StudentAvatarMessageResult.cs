using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record StudentAvatarMessageResult(Guid Id, int Position, AvatarMessageRole Role, string Text, DateTimeOffset CreatedAt, List<AvatarCitationResult> Citations);
