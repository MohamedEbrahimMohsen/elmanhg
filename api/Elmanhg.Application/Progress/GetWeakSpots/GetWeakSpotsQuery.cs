using Elmanhg.Application.Progress.Shared;
using MediatR;

namespace Elmanhg.Application.Progress.GetWeakSpots;

public sealed record GetWeakSpotsQuery : IRequest<WeakSpotsResult>;
