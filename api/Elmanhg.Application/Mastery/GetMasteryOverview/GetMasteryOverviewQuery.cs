using Elmanhg.Application.Mastery.Shared;
using MediatR;

namespace Elmanhg.Application.Mastery.GetMasteryOverview;

public sealed record GetMasteryOverviewQuery : IRequest<MasteryOverviewResult>;
