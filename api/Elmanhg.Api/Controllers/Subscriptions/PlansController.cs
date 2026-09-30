using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Subscriptions.GetPlanCatalogue;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Elmanhg.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/plans")]
[Authorize]
public class PlansController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetPlanCatalogue")]
    [AllowAnonymous]
    [EnableRateLimiting(PublicRateLimitPolicies.Reads)]
    [ProducesResponseType<PlanCatalogueResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPlanCatalogue(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPlanCatalogueQuery(), cancellationToken);
        return Ok(result);
    }
}
