using Core.DDD.Models;
using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Questions.CreateQuestion;
using Elmanhg.Application.Questions.GetQuestion;
using Elmanhg.Application.Questions.GetQuestions;
using Elmanhg.Application.Questions.GetServableQuestionCount;
using Elmanhg.Application.Questions.GradeQuestionDraft;
using Elmanhg.Application.Questions.ResubmitQuestion;
using Elmanhg.Application.Questions.RetireQuestion;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.UpdateQuestion;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Controllers.Questions;

[ApiController]
[Route("api/questions")]
[Authorize]
public class QuestionsController(IMediator mediator, IOptions<ContentOptions> contentOptions) : ControllerBase
{
    [HttpPost(Name = "CreateQuestion")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<CreateQuestionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateQuestion([FromBody] CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        var fields = new QuestionFields(request.Type, request.Stem, request.Body, request.GradingSpec, request.Explanation, request.Difficulty, request.ObjectiveId, request.Tags ?? [], request.MaxScore);
        var result = await mediator.Send(new CreateQuestionCommand(request.LessonId, fields), cancellationToken);
        return Ok(result);
    }

    [HttpGet("servable-count", Name = "GetServableQuestionCount")]
    [AllowAnonymous]
    [EnableRateLimiting(PublicRateLimitPolicies.Reads)]
    [ProducesResponseType<ServableQuestionCountResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetServableQuestionCount(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetServableQuestionCountQuery(), cancellationToken);
        Response.Headers.CacheControl = $"public, max-age={contentOptions.Value.ServableCountCacheSeconds}";
        return Ok(result);
    }

    [HttpGet("{questionId:guid}", Name = "GetQuestion")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<QuestionDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetQuestion([FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetQuestionQuery(questionId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{questionId:guid}", Name = "UpdateQuestion")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateQuestion([FromRoute] Guid questionId, [FromBody] UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var fields = new QuestionFields(request.Type, request.Stem, request.Body, request.GradingSpec, request.Explanation, request.Difficulty, request.ObjectiveId, request.Tags ?? [], request.MaxScore);
        await mediator.Send(new UpdateQuestionCommand(questionId, fields), cancellationToken);
        return Ok();
    }

    [HttpGet(Name = "GetQuestions")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<PageData<QuestionListItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetQuestions([FromQuery] QuestionValidationStatus? status, [FromQuery] QuestionType? type, [FromQuery] Guid? subjectId, [FromQuery] Guid? lessonId, [FromQuery] Guid? teacherId, [FromQuery] int? minVersion, [FromQuery] string? rejectionReason, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetQuestionsQuery(status, type, subjectId, lessonId, teacherId, minVersion, rejectionReason, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{questionId:guid}/resubmit", Name = "ResubmitQuestion")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResubmitQuestion([FromRoute] Guid questionId, [FromBody] UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var fields = new QuestionFields(request.Type, request.Stem, request.Body, request.GradingSpec, request.Explanation, request.Difficulty, request.ObjectiveId, request.Tags ?? [], request.MaxScore);
        await mediator.Send(new ResubmitQuestionCommand(questionId, fields), cancellationToken);
        return Ok();
    }

    [HttpPost("{questionId:guid}/retire", Name = "RetireQuestion")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RetireQuestion([FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RetireQuestionCommand(questionId), cancellationToken);
        return Ok();
    }

    [HttpPost("grade-draft", Name = "GradeQuestionDraft")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<QuestionGradeResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GradeQuestionDraft([FromBody] GradeQuestionDraftRequest request, CancellationToken cancellationToken)
    {
        var fields = new QuestionFields(request.Type, request.Stem, request.Body, request.GradingSpec, request.Explanation, request.Difficulty, request.ObjectiveId, request.Tags ?? [], request.MaxScore);
        var result = await mediator.Send(new GradeQuestionDraftQuery(fields, request.Answer, request.LessonId), cancellationToken);
        return Ok(result);
    }
}
