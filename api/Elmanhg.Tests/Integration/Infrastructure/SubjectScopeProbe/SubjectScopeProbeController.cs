using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Tests.Integration.Infrastructure.SubjectScopeProbe;

[ApiController]
[Route("api/test/subjects")]
[Authorize]
public class SubjectScopeProbeController(IMediator mediator) : ControllerBase
{
    [HttpGet("{subjectId:guid}/content")]
    [Authorize(Policy = DefaultCodes.ContentBrowse)]
    public async Task<ActionResult> Read([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new ReadSubjectContentProbeQuery(subjectId), cancellationToken));
    }

    [HttpPost("{subjectId:guid}/content")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    public async Task<ActionResult> Mutate([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new MutateSubjectContentProbeCommand(subjectId), cancellationToken));
    }
}
