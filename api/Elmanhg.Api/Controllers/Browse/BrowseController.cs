using Elmanhg.Application.Browse.GetStudentLesson;
using Elmanhg.Application.Browse.GetStudentSubject;
using Elmanhg.Application.Browse.GetStudentUnit;
using Elmanhg.Application.Browse.RecordLessonOpening;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Browse;

[ApiController]
[Route("api/browse")]
[Authorize]
public class BrowseController(IMediator mediator) : ControllerBase
{
    [HttpGet("subjects/{subjectId:guid}", Name = "GetStudentSubject")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<StudentSubjectResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudentSubject([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentSubjectQuery(subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("units/{unitId:guid}", Name = "GetStudentUnit")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<StudentUnitResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudentUnit([FromRoute] Guid unitId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentUnitQuery(unitId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("lessons/{lessonId:guid}", Name = "GetStudentLesson")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType<StudentLessonResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudentLesson([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentLessonQuery(lessonId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("lessons/{lessonId:guid}/openings", Name = "RecordLessonOpening")]
    [Authorize(Policy = DefaultCodes.ProgressViewOwn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RecordLessonOpening([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RecordLessonOpeningCommand(lessonId), cancellationToken);
        return Ok();
    }
}
