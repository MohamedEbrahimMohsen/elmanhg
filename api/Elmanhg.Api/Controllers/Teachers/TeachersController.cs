using Elmanhg.Application.Teachers.AssignTeacherSubject;
using Elmanhg.Application.Teachers.GetTeachers;
using Elmanhg.Application.Teachers.SetTeacherPhoneNumber;
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
    [HttpGet(Name = "GetTeachers")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType<List<TeacherResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeachers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTeachersQuery(), cancellationToken);
        return Ok(result);
    }

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

    [HttpPut("{teacherId:guid}/phone-number", Name = "SetTeacherPhoneNumber")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetPhoneNumber([FromRoute] Guid teacherId, [FromBody] SetTeacherPhoneNumberRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetTeacherPhoneNumberCommand(teacherId, request.PhoneNumber), cancellationToken);
        return Ok();
    }
}
