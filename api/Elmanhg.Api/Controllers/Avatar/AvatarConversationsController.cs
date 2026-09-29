using Core.DDD.Models;
using Elmanhg.Application.Avatar.GetAvatarConversation;
using Elmanhg.Application.Avatar.GetAvatarConversations;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Avatar;

[ApiController]
[Route("api/avatar/conversations")]
[Authorize]
public class AvatarConversationsController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetAvatarConversations")]
    [Authorize(Policy = DefaultCodes.AvatarConversationsView)]
    [ProducesResponseType<PageData<AdminAvatarConversationResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetConversations([FromQuery] string? search, [FromQuery] AvatarEntryPoint? entryPoint, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAvatarConversationsQuery(search, entryPoint, from, to, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{conversationId:guid}", Name = "GetAvatarConversation")]
    [Authorize(Policy = DefaultCodes.AvatarConversationsView)]
    [ProducesResponseType<AdminAvatarConversationDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetConversation([FromRoute] Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAvatarConversationQuery(conversationId), cancellationToken);
        return Ok(result);
    }
}
