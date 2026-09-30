using Elmanhg.Application.EssayGrading.GetEssayGrade;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.MathStepGrading.GetMathStepGrade;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Sessions.FinishSession;
using Elmanhg.Application.Sessions.GetSession;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.StartQuizSession;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Sessions;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController(IMediator mediator) : ControllerBase
{
    [HttpPost("quiz", Name = "StartQuizSession")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> StartQuizSession([FromBody] StartQuizSessionCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{sessionId:guid}", Name = "GetSession")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSession([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSessionQuery(sessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{sessionId:guid}/answers", Name = "SubmitSessionAnswer")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<SessionItemResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SubmitAnswer([FromRoute] Guid sessionId, [FromBody] SubmitAnswerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitAnswerCommand(sessionId, request.QuestionId, request.Answer, request.TimeTakenMilliseconds), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{sessionId:guid}/finish", Name = "FinishSession")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> FinishSession([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new FinishSessionCommand(sessionId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{sessionId:guid}/questions/{questionId:guid}/essay-grade", Name = "GetEssayGrade")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<EssayGradeResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetEssayGrade([FromRoute] Guid sessionId, [FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetEssayGradeQuery(sessionId, questionId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{sessionId:guid}/questions/{questionId:guid}/math-step-grade", Name = "GetMathStepGrade")]
    [Authorize(Policy = DefaultCodes.AssessmentsTake)]
    [ProducesResponseType<MathStepGradeResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMathStepGrade([FromRoute] Guid sessionId, [FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMathStepGradeQuery(sessionId, questionId), cancellationToken);
        return Ok(result);
    }
}
