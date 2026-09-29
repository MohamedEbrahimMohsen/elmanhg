using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Analytics.RecordFunnelEvent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Elmanhg.Api.Controllers.Analytics;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController(IMediator mediator) : ControllerBase
{
    [HttpPost("funnel-events", Name = "RecordFunnelEvent")]
    [AllowAnonymous]
    [EnableRateLimiting(AnalyticsRateLimitPolicies.FunnelEvents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RecordFunnelEvent([FromBody] RecordFunnelEventRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RecordFunnelEventCommand(request.AnonymousId, request.Type), cancellationToken);
        return Ok();
    }
}
