using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetMyUsage;

public sealed record GetMyUsageQuery : IRequest<UsageResult>;
