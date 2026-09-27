using Core.DDD.Models;
using Elmanhg.Application.AuditLogs.GetAuditLogResourceTypes;
using Elmanhg.Application.AuditLogs.GetAuditLogs;
using Elmanhg.Application.AuditLogs.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.AuditLogs;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetAuditLogs")]
    [Authorize(Policy = DefaultCodes.AuditLogView)]
    [ProducesResponseType<PageData<AuditLogResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuditLogs([FromQuery] string? actor, [FromQuery] string? resourceType, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAuditLogsQuery(actor, resourceType, from, to, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("resource-types", Name = "GetAuditLogResourceTypes")]
    [Authorize(Policy = DefaultCodes.AuditLogView)]
    [ProducesResponseType<List<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetResourceTypes(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAuditLogResourceTypesQuery(), cancellationToken);
        return Ok(result);
    }
}
