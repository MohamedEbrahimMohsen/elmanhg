using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Observability.ReportClientError;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Elmanhg.Api.Controllers.Observability;

[ApiController]
[Route("api/client-errors")]
[Authorize]
public class ClientErrorsController(IMediator mediator) : ControllerBase
{
    private const long MaxReportBytes = 16384;

    [HttpPost(Name = "ReportClientError")]
    [AllowAnonymous]
    [EnableRateLimiting(ObservabilityRateLimitPolicies.ClientErrors)]
    [RequestSizeLimit(MaxReportBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Report([FromBody] ReportClientErrorRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReportClientErrorCommand(request.Message, request.ErrorName, request.Stack, request.Source, request.Path), cancellationToken);
        return Ok();
    }
}
