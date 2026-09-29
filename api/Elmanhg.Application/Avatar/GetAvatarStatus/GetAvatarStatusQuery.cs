using Elmanhg.Application.Avatar.Shared;
using MediatR;

namespace Elmanhg.Application.Avatar.GetAvatarStatus;

public sealed record GetAvatarStatusQuery : IRequest<AvatarStatusResult>;
