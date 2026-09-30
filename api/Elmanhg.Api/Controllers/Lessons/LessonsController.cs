using Elmanhg.Application.Lessons.ArchiveLesson;
using Elmanhg.Application.Lessons.CreateLesson;
using Elmanhg.Application.Lessons.DeleteLesson;
using Elmanhg.Application.Lessons.GetLesson;
using Elmanhg.Application.Lessons.GetLessons;
using Elmanhg.Application.Lessons.PublishLesson;
using Elmanhg.Application.Lessons.ReorderLesson;
using Elmanhg.Application.Lessons.Shared;
using Elmanhg.Application.Lessons.UnpublishLesson;
using Elmanhg.Application.Lessons.UpdateLesson;
using Elmanhg.Application.Lessons.UploadDiagramImage;
using Elmanhg.Application.Lessons.UploadLessonImage;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Lessons;

[ApiController]
[Route("api/lessons")]
[Authorize]
public class LessonsController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetLessons")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<List<LessonResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetLessons([FromQuery] Guid unitId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLessonsQuery(unitId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{lessonId:guid}", Name = "GetLesson")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<LessonDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetLesson([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLessonQuery(lessonId), cancellationToken);
        return Ok(result);
    }

    [HttpPost(Name = "CreateLesson")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<CreateLessonResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateLesson([FromBody] CreateLessonRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateLessonCommand(request.UnitId, request.Name), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{lessonId:guid}", Name = "UpdateLesson")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateLesson([FromRoute] Guid lessonId, [FromBody] UpdateLessonRequest request, CancellationToken cancellationToken)
    {
        var objectives = request.Objectives
            .Select(x => new LessonObjectiveContent(x.Id, x.Text))
            .ToList();
        await mediator.Send(new UpdateLessonCommand(lessonId, request.Name, request.Explanation, request.Summary, request.VideoUrl, objectives), cancellationToken);
        return Ok();
    }

    [HttpPost("{lessonId:guid}/publish", Name = "PublishLesson")]
    [Authorize(Policy = DefaultCodes.LessonsPublish)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> PublishLesson([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        await mediator.Send(new PublishLessonCommand(lessonId), cancellationToken);
        return Ok();
    }

    [HttpPost("{lessonId:guid}/unpublish", Name = "UnpublishLesson")]
    [Authorize(Policy = DefaultCodes.LessonsPublish)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UnpublishLesson([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnpublishLessonCommand(lessonId), cancellationToken);
        return Ok();
    }

    [HttpPost("{lessonId:guid}/archive", Name = "ArchiveLesson")]
    [Authorize(Policy = DefaultCodes.LessonsPublish)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ArchiveLesson([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        await mediator.Send(new ArchiveLessonCommand(lessonId), cancellationToken);
        return Ok();
    }

    [HttpPut("{lessonId:guid}/position", Name = "ReorderLesson")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReorderLesson([FromRoute] Guid lessonId, [FromBody] LessonPositionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReorderLessonCommand(lessonId, request.Position), cancellationToken);
        return Ok();
    }

    [HttpDelete("{lessonId:guid}", Name = "DeleteLesson")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteLesson([FromRoute] Guid lessonId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteLessonCommand(lessonId), cancellationToken);
        return Ok();
    }

    [HttpPost("{lessonId:guid}/images", Name = "UploadLessonImage")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadLessonImageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UploadLessonImage([FromRoute] Guid lessonId, IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UploadLessonImageCommand(lessonId, file), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{lessonId:guid}/diagram-images", Name = "UploadDiagramImage")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadDiagramImageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UploadDiagramImage([FromRoute] Guid lessonId, IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UploadDiagramImageCommand(lessonId, file), cancellationToken);
        return Ok(result);
    }
}
