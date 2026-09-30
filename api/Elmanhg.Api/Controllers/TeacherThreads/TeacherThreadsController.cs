using Core.DDD.Models;
using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;
using Elmanhg.Application.TeacherThreads.GetMyTeacherThread;
using Elmanhg.Application.TeacherThreads.GetMyTeacherThreads;
using Elmanhg.Application.TeacherThreads.GetTeacherThreadContext;
using Elmanhg.Application.TeacherThreads.MarkTeacherThreadRead;
using Elmanhg.Application.TeacherThreads.RateTeacherThread;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Elmanhg.Api.Controllers.TeacherThreads;

[ApiController]
[Route("api/teacher-threads")]
[Authorize]
public class TeacherThreadsController(IMediator mediator) : ControllerBase
{
    [HttpGet("context", Name = "GetTeacherThreadContext")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [ProducesResponseType<TeacherThreadContextResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeacherThreadContext([FromQuery] Guid? lessonId, [FromQuery] Guid? questionId, [FromQuery] Guid? attemptId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTeacherThreadContextQuery(lessonId, questionId, attemptId), cancellationToken);
        return Ok(result);
    }

    [HttpGet(Name = "GetMyTeacherThreads")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [ProducesResponseType<PageData<TeacherThreadSummaryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyTeacherThreads([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetMyTeacherThreadsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{threadId:guid}", Name = "GetMyTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [ProducesResponseType<TeacherThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMyTeacherThread([FromRoute] Guid threadId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMyTeacherThreadQuery(threadId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/read", Name = "MarkTeacherThreadRead")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> MarkTeacherThreadRead([FromRoute] Guid threadId, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkTeacherThreadReadCommand(threadId), cancellationToken);
        return Ok();
    }

    [HttpPost("{threadId:guid}/follow-ups", Name = "FollowUpTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [EnableRateLimiting(StudentRateLimitPolicies.AskTeacherSubmissions)]
    [ProducesResponseType<TeacherThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> FollowUpTeacherThread([FromRoute] Guid threadId, [FromBody] FollowUpTeacherThreadRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new FollowUpTeacherThreadCommand(threadId, request.Text), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/rating", Name = "RateTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [ProducesResponseType<TeacherThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RateTeacherThread([FromRoute] Guid threadId, [FromBody] RateTeacherThreadRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RateTeacherThreadCommand(threadId, request.Rating), cancellationToken);
        return Ok(result);
    }

    [HttpPost(Name = "CreateTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherSubmit)]
    [EnableRateLimiting(StudentRateLimitPolicies.AskTeacherSubmissions)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<TeacherThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateTeacherThread([FromForm] string? text, [FromForm] Guid? lessonId, [FromForm] Guid? questionId, [FromForm] Guid? attemptId, IFormFile? image, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateTeacherThreadCommand(text, lessonId, questionId, attemptId, image), cancellationToken);
        return Ok(result);
    }
}
