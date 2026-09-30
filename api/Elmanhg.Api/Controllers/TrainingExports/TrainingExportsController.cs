using Core.DDD.Models;
using Elmanhg.Application.TrainingExports.DownloadTrainingExport;
using Elmanhg.Application.TrainingExports.GetTrainingExports;
using Elmanhg.Application.TrainingExports.RequestTrainingExport;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.TrainingExports;

[ApiController]
[Route("api/training-exports")]
[Authorize]
public class TrainingExportsController(IMediator mediator) : ControllerBase
{
    [HttpPost(Name = "RequestTrainingExport")]
    [Authorize(Policy = DefaultCodes.TrainingDataExport)]
    [ProducesResponseType<TrainingExportResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RequestTrainingExport([FromBody] RequestTrainingExportRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RequestTrainingExportCommand(request.Source, request.From, request.To, request.SubjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet(Name = "GetTrainingExports")]
    [Authorize(Policy = DefaultCodes.TrainingDataExport)]
    [ProducesResponseType<PageData<TrainingExportResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTrainingExports([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTrainingExportsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{exportId:guid}/file", Name = "DownloadTrainingExportFile")]
    [Authorize(Policy = DefaultCodes.TrainingDataExport)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "application/x-ndjson")]
    public async Task<ActionResult> DownloadTrainingExportFile([FromRoute] Guid exportId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DownloadTrainingExportQuery(exportId), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Content, result.ContentType, result.FileName);
    }
}
