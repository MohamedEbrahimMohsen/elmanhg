using Elmanhg.Application.Questions.GetQuestionImportTemplate;
using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Application.Questions.PreviewQuestionImport;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Questions;

[ApiController]
[Route("api/question-imports")]
[Authorize]
public class QuestionImportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("template", Name = "GetQuestionImportTemplate")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, QuestionImportFile.ContentType)]
    public async Task<ActionResult> GetQuestionImportTemplate(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetQuestionImportTemplateQuery(), cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("preview", Name = "PreviewQuestionImport")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<QuestionImportPreviewResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> PreviewQuestionImport([FromForm] Guid lessonId, IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PreviewQuestionImportQuery(lessonId, file), cancellationToken);
        return Ok(result);
    }

    [HttpPost(Name = "ImportQuestions")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImportQuestionsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ImportQuestions([FromForm] Guid lessonId, [FromForm] Guid batchId, IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ImportQuestionsCommand(lessonId, batchId, file), cancellationToken);
        return Ok(result);
    }
}
