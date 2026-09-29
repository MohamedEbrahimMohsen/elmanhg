using Elmanhg.Domain.Analytics;

namespace Elmanhg.Api.Controllers.Analytics;

public sealed record RecordFunnelEventRequest(Guid AnonymousId, FunnelEventType Type);
