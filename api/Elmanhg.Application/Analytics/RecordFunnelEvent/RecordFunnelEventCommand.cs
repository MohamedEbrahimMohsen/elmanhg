using Elmanhg.Domain.Analytics;
using MediatR;

namespace Elmanhg.Application.Analytics.RecordFunnelEvent;

public sealed record RecordFunnelEventCommand(Guid AnonymousId, FunnelEventType Type) : IRequest;
