using Core.DDD.Models;
using Elmanhg.Application.TeacherInbox.ClaimTeacherThread;
using Elmanhg.Application.TeacherInbox.GetInboxThread;
using Elmanhg.Application.TeacherInbox.GetTeacherInbox;
using Elmanhg.Application.TeacherInbox.ReplyToTeacherThread;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.TeacherInbox;

[ApiController]
[Route("api/teacher-inbox")]
[Authorize]
public class TeacherInboxController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetTeacherInbox")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<PageData<TeacherInboxItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeacherInbox([FromQuery] TeacherInboxFilter filter = TeacherInboxFilter.All, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTeacherInboxQuery(filter, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{threadId:guid}", Name = "GetInboxThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetInboxThread([FromRoute] Guid threadId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInboxThreadQuery(threadId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/claim", Name = "ClaimTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ClaimTeacherThread([FromRoute] Guid threadId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ClaimTeacherThreadCommand(threadId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/replies", Name = "ReplyToTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReplyToTeacherThread([FromRoute] Guid threadId, [FromBody] ReplyToTeacherThreadRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReplyToTeacherThreadCommand(threadId, request.Text), cancellationToken);
        return Ok(result);
    }
}
