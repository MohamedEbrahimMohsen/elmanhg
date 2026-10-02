using Core.DDD.Models;
using Elmanhg.Application.Payments.GetPaymentLog;
using Elmanhg.Application.Payments.GetPaymentSettings;
using Elmanhg.Application.Payments.RefundPayment;
using Elmanhg.Application.Payments.ResolvePaymentReview;
using Elmanhg.Application.Payments.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Payments;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(IMediator mediator) : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    [HttpGet(Name = "GetPaymentLog")]
    [Authorize(Policy = DefaultCodes.PaymentsManage)]
    [ProducesResponseType<PageData<AdminPaymentResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPaymentLog([FromQuery] PaymentStatus? status, [FromQuery] SubscriptionPlan? plan, [FromQuery] Guid? studentId, [FromQuery] string? reference, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] bool needsReview = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetPaymentLogQuery(status, plan, needsReview, studentId, reference, from, to, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("settings", Name = "GetPaymentSettings")]
    [Authorize(Policy = DefaultCodes.PaymentsManage)]
    [ProducesResponseType<PaymentSettingsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPaymentSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPaymentSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{paymentId:guid}/refund", Name = "RefundPayment")]
    [Authorize(Policy = DefaultCodes.PaymentsManage)]
    [ProducesResponseType<AdminPaymentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RefundPayment([FromRoute] Guid paymentId, [FromBody] RefundPaymentRequest request, [FromHeader(Name = IdempotencyKeyHeader)] Guid? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RefundPaymentCommand(paymentId, request.Reason, idempotencyKey), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{paymentId:guid}/review-resolution", Name = "ResolvePaymentReview")]
    [Authorize(Policy = DefaultCodes.PaymentsManage)]
    [ProducesResponseType<AdminPaymentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResolvePaymentReview([FromRoute] Guid paymentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResolvePaymentReviewCommand(paymentId), cancellationToken);
        return Ok(result);
    }
}
