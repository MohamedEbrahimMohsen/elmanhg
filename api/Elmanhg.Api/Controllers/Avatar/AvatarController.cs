using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Avatar.GetAvatarStatus;
using Elmanhg.Application.Avatar.SendAvatarMessage;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Elmanhg.Api.Controllers.Avatar;

[ApiController]
[Route("api/avatar")]
[Authorize]
public class AvatarController(IMediator mediator) : ControllerBase
{
    [HttpGet("status", Name = "GetAvatarStatus")]
    [Authorize(Policy = DefaultCodes.AvatarChat)]
    [ProducesResponseType<AvatarStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAvatarStatusQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("messages", Name = "SendAvatarMessage")]
    [Authorize(Policy = DefaultCodes.AvatarChat)]
    [EnableRateLimiting(StudentRateLimitPolicies.AvatarMessages)]
    [ProducesResponseType<AvatarReplyResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendMessage([FromBody] SendAvatarMessageCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
