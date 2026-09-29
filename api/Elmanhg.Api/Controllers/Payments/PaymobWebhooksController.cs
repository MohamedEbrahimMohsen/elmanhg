using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Elmanhg.Api.Controllers.Payments;

[ApiController]
[Route("api/payments/paymob")]
[ApiExplorerSettings(IgnoreApi = true)]
public class PaymobWebhooksController(IMediator mediator) : ControllerBase
{
    private const long MaxNotificationBytes = 65536;

    [HttpPost("webhook", Name = "ProcessPaymobWebhook")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxNotificationBytes)]
    public async Task<ActionResult> ProcessWebhook([FromBody] JsonElement payload, [FromQuery] string? hmac, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ProcessPaymentNotificationCommand(payload.GetRawText(), hmac), cancellationToken);
        return Ok(result);
    }
}
