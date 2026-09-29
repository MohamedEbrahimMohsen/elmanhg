using Elmanhg.Application.Subscriptions.GetPlanCatalogue;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/plans")]
[Authorize]
public class PlansController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetPlanCatalogue")]
    [AllowAnonymous]
    [ProducesResponseType<PlanCatalogueResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPlanCatalogue(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPlanCatalogueQuery(), cancellationToken);
        return Ok(result);
    }
}
