using Elmanhg.Application.Questions.CreateQuestion;
using Elmanhg.Application.Questions.GetQuestion;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.UpdateQuestion;
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
}
