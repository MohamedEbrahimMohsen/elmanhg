using Core.DDD.Models;
using Elmanhg.Application.Progress.GetStudentProgress;
using Elmanhg.Application.Progress.GetStudentSessionHistory;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Application.Students.GetStudentProfile;
using Elmanhg.Application.Students.GetSubjectInterests;
using Elmanhg.Application.Students.SaveSubjectInterests;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Application.Subscriptions.GrantComplimentarySubscription;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Sessions;
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

    [HttpGet("{studentId:guid}", Name = "GetStudentProfile")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType<StudentProfileResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudentProfile([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentProfileQuery(studentId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{studentId:guid}/progress", Name = "GetStudentProgress")]
    [Authorize(Policy = DefaultCodes.ProgressViewAny)]
    [ProducesResponseType<StudentProgressResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudentProgress([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentProgressQuery(studentId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{studentId:guid}/sessions", Name = "GetStudentSessionHistory")]
    [Authorize(Policy = DefaultCodes.ProgressViewAny)]
    [ProducesResponseType<PageData<SessionHistoryItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudentSessionHistory([FromRoute] Guid studentId, [FromQuery] SessionHistoryKind? kind, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetStudentSessionHistoryQuery(studentId, kind, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{studentId:guid}/complimentary-subscriptions", Name = "GrantComplimentarySubscription")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType<AdminSubscriptionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GrantComplimentarySubscription([FromRoute] Guid studentId, [FromBody] GrantComplimentarySubscriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GrantComplimentarySubscriptionCommand(studentId, request.Plan, request.Period), cancellationToken);
        return Ok(result);
    }
}
