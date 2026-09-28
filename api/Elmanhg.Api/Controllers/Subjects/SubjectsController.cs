using Elmanhg.Application.Subjects.CreateSubject;
using Elmanhg.Application.Subjects.DeleteSubject;
using Elmanhg.Application.Subjects.GetSubject;
using Elmanhg.Application.Subjects.GetSubjects;
using Elmanhg.Application.Subjects.ReorderSubject;
using Elmanhg.Application.Subjects.Shared;
using Elmanhg.Application.Subjects.UpdateSubject;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Subjects;

[ApiController]
[Route("api/subjects")]
[Authorize]
public class SubjectsController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetSubjects")]
    [Authorize(Policy = DefaultCodes.ContentBrowse)]
    [ProducesResponseType<List<SubjectResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubjects(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{subjectId:guid}", Name = "GetSubject")]
    [Authorize(Policy = DefaultCodes.ContentBrowse)]
    [ProducesResponseType<SubjectDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubject([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectQuery(subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpPost(Name = "CreateSubject")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<CreateSubjectResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateSubject([FromBody] SubjectNameRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateSubjectCommand(request.Name), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{subjectId:guid}", Name = "UpdateSubject")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateSubject([FromRoute] Guid subjectId, [FromBody] SubjectNameRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSubjectCommand(subjectId, request.Name), cancellationToken);
        return Ok();
    }

    [HttpPut("{subjectId:guid}/position", Name = "ReorderSubject")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReorderSubject([FromRoute] Guid subjectId, [FromBody] SubjectPositionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReorderSubjectCommand(subjectId, request.Position), cancellationToken);
        return Ok();
    }

    [HttpDelete("{subjectId:guid}", Name = "DeleteSubject")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteSubject([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteSubjectCommand(subjectId), cancellationToken);
        return Ok();
    }
}
