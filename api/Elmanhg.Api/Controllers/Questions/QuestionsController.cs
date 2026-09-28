using Core.DDD.Models;
using Elmanhg.Application.Questions.CreateQuestion;
using Elmanhg.Application.Questions.GetQuestion;
using Elmanhg.Application.Questions.GetQuestions;
using Elmanhg.Application.Questions.GradeQuestionDraft;
using Elmanhg.Application.Questions.ResubmitQuestion;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.UpdateQuestion;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Questions;

[ApiController]
[Route("api/questions")]
[Authorize]
public class QuestionsController(IMediator mediator) : ControllerBase
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

    [HttpPost("grade-draft", Name = "GradeQuestionDraft")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<QuestionGradeResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GradeQuestionDraft([FromBody] GradeQuestionDraftRequest request, CancellationToken cancellationToken)
    {
        var fields = new QuestionFields(request.Type, request.Stem, request.Body, request.GradingSpec, request.Explanation, request.Difficulty, request.ObjectiveId, request.Tags ?? [], request.MaxScore);
        var result = await mediator.Send(new GradeQuestionDraftQuery(fields, request.Answer), cancellationToken);
        return Ok(result);
    }
}
