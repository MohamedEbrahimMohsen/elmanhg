using Elmanhg.Application.ContentRetrieval.RebuildContentIndex;
using Elmanhg.Application.ContentRetrieval.SearchLessonContent;
using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.ContentRetrieval;

[ApiController]
[Route("api")]
[Authorize]
public class ContentRetrievalController(IMediator mediator) : ControllerBase
{
    [HttpPost("lessons/{lessonId:guid}/content-chunks/search", Name = "SearchLessonContent")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<LessonContentSearchResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Search([FromRoute] Guid lessonId, [FromBody] SearchLessonContentRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SearchLessonContentQuery(lessonId, request.Query, request.Top, request.IncludeQuestionExplanations ?? true), cancellationToken);
        return Ok(result);
    }

    [HttpPost("content-index/rebuild", Name = "RebuildContentIndex")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Rebuild(CancellationToken cancellationToken)
    {
        await mediator.Send(new RebuildContentIndexCommand(), cancellationToken);
        return Ok();
    }
}
