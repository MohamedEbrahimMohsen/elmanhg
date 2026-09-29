using Elmanhg.Application.Students.GetSubjectInterests;
using Elmanhg.Application.Students.SaveSubjectInterests;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Students;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController(IMediator mediator) : ControllerBase
{
    [HttpGet("me/subject-interests", Name = "GetSubjectInterests")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<SubjectInterestsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubjectInterests(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectInterestsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/subject-interests", Name = "SaveSubjectInterests")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveSubjectInterests([FromBody] SubjectInterestsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SaveSubjectInterestsCommand(request.SubjectIds ?? []), cancellationToken);
        return Ok();
    }
}
