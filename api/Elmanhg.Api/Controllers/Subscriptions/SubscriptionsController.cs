using Core.DDD.Models;
using Elmanhg.Application.Subscriptions.GetMyEntitlement;
using Elmanhg.Application.Subscriptions.GetMyPayments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController(IMediator mediator) : ControllerBase
{
    [HttpGet("entitlement", Name = "GetMyEntitlement")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<EntitlementResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyEntitlement(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMyEntitlementQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments", Name = "GetMyPayments")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<PageData<PaymentResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyPayments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetMyPaymentsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }
}
