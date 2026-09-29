using MediatR;

namespace Elmanhg.Application.Subscriptions.GetLapsedSubscriptionIds;

public sealed record GetLapsedSubscriptionIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;
