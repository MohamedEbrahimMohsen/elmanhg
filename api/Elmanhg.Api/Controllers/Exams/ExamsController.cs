using Elmanhg.Application.Exams.GetExamAttempts;
using Elmanhg.Application.Exams.GetExamSession;
using Elmanhg.Application.Exams.GetMultiUnitExamOverview;
using Elmanhg.Application.Exams.GetUnitExamAttempts;
using Elmanhg.Application.Exams.GetUnitExamOverview;
using Elmanhg.Application.Exams.PreviewMultiUnitExam;
using Elmanhg.Application.Exams.SaveExamAnswer;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exams.StartMultiUnitExam;
using Elmanhg.Application.Exams.StartUnitExam;
using Elmanhg.Application.Exams.SubmitExam;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Exams;

[ApiController]
[Route("api/exams")]
[Authorize]
public class ExamsController(IMediator mediator) : ControllerBase
{
    [HttpGet("units/{unitId:guid}", Name = "GetUnitExamOverview")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<UnitExamOverviewResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUnitExamOverview([FromRoute] Guid unitId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUnitExamOverviewQuery(unitId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("units/{unitId:guid}", Name = "StartUnitExam")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamSessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> StartUnitExam([FromRoute] Guid unitId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new StartUnitExamCommand(unitId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("units/{unitId:guid}/attempts", Name = "GetUnitExamAttempts")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamAttemptsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUnitExamAttempts([FromRoute] Guid unitId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUnitExamAttemptsQuery(unitId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subjects/{subjectId:guid}/multi-unit", Name = "GetMultiUnitExamOverview")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<MultiUnitExamOverviewResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMultiUnitExamOverview([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMultiUnitExamOverviewQuery(subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subjects/{subjectId:guid}/multi-unit/preview", Name = "PreviewMultiUnitExam")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<MultiUnitExamPreviewResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> PreviewMultiUnitExam([FromRoute] Guid subjectId, [FromQuery] List<Guid>? unitIds, [FromQuery] int size, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PreviewMultiUnitExamQuery(new MultiUnitExamSelection(subjectId, unitIds, size)), cancellationToken);
        return Ok(result);
    }

    [HttpPost("subjects/{subjectId:guid}/multi-unit", Name = "StartMultiUnitExam")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamSessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> StartMultiUnitExam([FromRoute] Guid subjectId, [FromBody] StartMultiUnitExamRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new StartMultiUnitExamCommand(new MultiUnitExamSelection(subjectId, request.UnitIds, request.Size)), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{sessionId:guid}", Name = "GetExamSession")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamSessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetExamSession([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExamSessionQuery(sessionId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{sessionId:guid}/attempts", Name = "GetExamAttempts")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamAttemptsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetExamAttempts([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExamAttemptsQuery(sessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{sessionId:guid}/answers/{questionId:guid}", Name = "SaveExamAnswer")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamAnswerSavedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveExamAnswer([FromRoute] Guid sessionId, [FromRoute] Guid questionId, [FromBody] SaveExamAnswerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveExamAnswerCommand(sessionId, questionId, request.Answer), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{sessionId:guid}/submit", Name = "SubmitExam")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<ExamSessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SubmitExam([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitExamCommand(sessionId), cancellationToken);
        return Ok(result);
    }
}
