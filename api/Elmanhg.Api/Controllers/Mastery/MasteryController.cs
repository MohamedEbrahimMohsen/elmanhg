using Elmanhg.Application.Mastery.GetMasteryOverview;
using Elmanhg.Application.Mastery.GetSubjectMastery;
using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Mastery;

[ApiController]
[Route("api/mastery")]
[Authorize]
public class MasteryController(IMediator mediator) : ControllerBase
{
    [HttpGet("overview", Name = "GetMasteryOverview")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<MasteryOverviewResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMasteryOverview(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMasteryOverviewQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subjects/{subjectId:guid}", Name = "GetSubjectMastery")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<SubjectMasteryDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubjectMastery([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectMasteryQuery(subjectId), cancellationToken);
        return Ok(result);
    }
}
