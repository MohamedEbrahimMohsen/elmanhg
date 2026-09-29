using Core.DDD.Models;
using Elmanhg.Application.Subscriptions.CancelSubscription;
using Elmanhg.Application.Subscriptions.CompleteFakePayment;
using Elmanhg.Application.Subscriptions.GetMyEntitlement;
using Elmanhg.Application.Subscriptions.GetMyPayment;
using Elmanhg.Application.Subscriptions.GetMyPayments;
using Elmanhg.Application.Subscriptions.GetMyUsage;
using Elmanhg.Application.Subscriptions.StartCheckout;
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

    [HttpGet("usage", Name = "GetMyUsage")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<UsageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyUsage(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMyUsageQuery(), cancellationToken);
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

    [HttpPost("checkout", Name = "StartCheckout")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<CheckoutResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> StartCheckout([FromBody] StartCheckoutCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments/{paymentId:guid}", Name = "GetMyPayment")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyPayment(Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMyPaymentQuery(paymentId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("payments/{paymentId:guid}/fake-completion", Name = "CompleteFakePayment")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CompleteFakePayment(Guid paymentId, [FromBody] CompleteFakePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CompleteFakePaymentCommand(paymentId, request.Succeeded), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{subscriptionId:guid}/cancel", Name = "CancelSubscription")]
    [Authorize(Policy = DefaultCodes.SubscriptionManage)]
    [ProducesResponseType<EntitlementResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CancelSubscription([FromRoute] Guid subscriptionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelSubscriptionCommand(subscriptionId), cancellationToken);
        return Ok(result);
    }
}
