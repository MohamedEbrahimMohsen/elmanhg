using Elmanhg.Application.Teachers.AssignTeacherSubject;
using Elmanhg.Application.Teachers.Shared;
using Elmanhg.Application.Teachers.UnassignTeacherSubject;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Teachers;

[ApiController]
[Route("api/teachers")]
[Authorize]
public class TeachersController(IMediator mediator) : ControllerBase
{
    [HttpPost("{teacherId:guid}/subjects/{subjectId:guid}", Name = "AssignTeacherSubject")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType<TeacherSubjectResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AssignSubject([FromRoute] Guid teacherId, [FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AssignTeacherSubjectCommand(teacherId, subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{teacherId:guid}/subjects/{subjectId:guid}", Name = "UnassignTeacherSubject")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UnassignSubject([FromRoute] Guid teacherId, [FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnassignTeacherSubjectCommand(teacherId, subjectId), cancellationToken);
        return Ok();
    }
}
