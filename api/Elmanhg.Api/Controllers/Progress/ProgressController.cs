using Core.DDD.Models;
using Elmanhg.Application.Progress.GetSessionHistory;
using Elmanhg.Application.Progress.GetSubjectProgress;
using Elmanhg.Application.Progress.GetWeakSpots;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Progress;

[ApiController]
[Route("api/progress")]
[Authorize]
public class ProgressController(IMediator mediator) : ControllerBase
{
    [HttpGet("subjects", Name = "GetSubjectProgress")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<List<SubjectProgressResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubjectProgress(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectProgressQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("weak-spots", Name = "GetWeakSpots")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<WeakSpotsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetWeakSpots(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWeakSpotsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("sessions", Name = "GetSessionHistory")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<PageData<SessionHistoryItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSessionHistory([FromQuery] SessionHistoryKind? kind, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetSessionHistoryQuery(kind, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }
}
