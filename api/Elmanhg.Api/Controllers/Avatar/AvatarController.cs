using Core.DDD.Models;
using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Avatar.DeleteMyAvatarConversation;
using Elmanhg.Application.Avatar.GetAvatarStatus;
using Elmanhg.Application.Avatar.GetMyAvatarConversation;
using Elmanhg.Application.Avatar.GetMyAvatarConversations;
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

    [HttpGet("my-conversations", Name = "GetMyAvatarConversations")]
    [Authorize(Policy = DefaultCodes.AvatarChat)]
    [ProducesResponseType<PageData<StudentAvatarConversationResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyConversations([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetMyAvatarConversationsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-conversations/{conversationId:guid}", Name = "GetMyAvatarConversation")]
    [Authorize(Policy = DefaultCodes.AvatarChat)]
    [ProducesResponseType<StudentAvatarConversationDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyConversation([FromRoute] Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMyAvatarConversationQuery(conversationId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("my-conversations/{conversationId:guid}", Name = "DeleteMyAvatarConversation")]
    [Authorize(Policy = DefaultCodes.AvatarChat)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteMyConversation([FromRoute] Guid conversationId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteMyAvatarConversationCommand(conversationId), cancellationToken);
        return Ok();
    }
}
