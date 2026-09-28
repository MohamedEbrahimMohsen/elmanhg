using Core.DDD.Models;
using Elmanhg.Application.QuestionValidation.ApproveQuestion;
using Elmanhg.Application.QuestionValidation.BulkApproveQuestions;
using Elmanhg.Application.QuestionValidation.GetValidationQuestion;
using Elmanhg.Application.QuestionValidation.GetValidationQueue;
using Elmanhg.Application.QuestionValidation.GetValidationQueueFilters;
using Elmanhg.Application.QuestionValidation.RecordQuestionOpening;
using Elmanhg.Application.QuestionValidation.RejectQuestion;
using Elmanhg.Application.QuestionValidation.Shared;
using Elmanhg.Application.QuestionValidation.StartReviewSession;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.QuestionValidation;

[ApiController]
[Route("api/validation-queue")]
[Authorize]
public class ValidationQueueController(IMediator mediator) : ControllerBase
{
    [HttpPost("review-sessions", Name = "StartReviewSession")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType<ReviewSessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> StartReviewSession(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new StartReviewSessionCommand(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("filters", Name = "GetValidationQueueFilters")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType<ValidationQueueFiltersResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetValidationQueueFilters(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetValidationQueueFiltersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet(Name = "GetValidationQueue")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType<PageData<ValidationQueueItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetValidationQueue([FromQuery] Guid? unitId, [FromQuery] Guid? lessonId, [FromQuery] QuestionType? type, [FromQuery] QuestionDifficulty? difficulty, [FromQuery] int? minAgeDays, [FromQuery] Guid? reviewSessionId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetValidationQueueQuery(unitId, lessonId, type, difficulty, minAgeDays, reviewSessionId, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("questions/{questionId:guid}", Name = "GetValidationQuestion")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType<ValidationQuestionDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetValidationQuestion([FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetValidationQuestionQuery(questionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("review-sessions/{reviewSessionId:guid}/openings/{questionId:guid}", Name = "RecordQuestionOpening")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RecordQuestionOpening([FromRoute] Guid reviewSessionId, [FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RecordQuestionOpeningCommand(reviewSessionId, questionId), cancellationToken);
        return Ok();
    }

    [HttpPost("questions/{questionId:guid}/approve", Name = "ApproveQuestion")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ApproveQuestion([FromRoute] Guid questionId, [FromBody] ApproveQuestionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveQuestionCommand(questionId, request.Version.GetValueOrDefault(), request.Difficulty), cancellationToken);
        return Ok();
    }

    [HttpPost("questions/{questionId:guid}/reject", Name = "RejectQuestion")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RejectQuestion([FromRoute] Guid questionId, [FromBody] RejectQuestionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectQuestionCommand(questionId, request.Version.GetValueOrDefault(), request.Reason), cancellationToken);
        return Ok();
    }

    [HttpPost("bulk-approve", Name = "BulkApproveQuestions")]
    [Authorize(Policy = DefaultCodes.QuestionsValidate)]
    [ProducesResponseType<BulkApproveQuestionsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> BulkApproveQuestions([FromBody] BulkApproveQuestionsRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new BulkApproveQuestionsCommand(request.ReviewSessionId, request.QuestionIds ?? []), cancellationToken);
        return Ok(result);
    }
}
