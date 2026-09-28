using Elmanhg.Application.Units.CreateUnit;
using Elmanhg.Application.Units.DeleteUnit;
using Elmanhg.Application.Units.ReorderUnit;
using Elmanhg.Application.Units.UpdateUnit;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Units;

[ApiController]
[Route("api/subjects/{subjectId:guid}/units")]
[Authorize]
public class UnitsController(IMediator mediator) : ControllerBase
{
    [HttpPost(Name = "CreateUnit")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType<CreateUnitResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateUnit([FromRoute] Guid subjectId, [FromBody] UnitNameRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateUnitCommand(subjectId, request.Name), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{unitId:guid}", Name = "UpdateUnit")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateUnit([FromRoute] Guid subjectId, [FromRoute] Guid unitId, [FromBody] UnitNameRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateUnitCommand(subjectId, unitId, request.Name), cancellationToken);
        return Ok();
    }

    [HttpPut("{unitId:guid}/position", Name = "ReorderUnit")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReorderUnit([FromRoute] Guid subjectId, [FromRoute] Guid unitId, [FromBody] UnitPositionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReorderUnitCommand(subjectId, unitId, request.Position), cancellationToken);
        return Ok();
    }

    [HttpDelete("{unitId:guid}", Name = "DeleteUnit")]
    [Authorize(Policy = DefaultCodes.ContentManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteUnit([FromRoute] Guid subjectId, [FromRoute] Guid unitId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUnitCommand(subjectId, unitId), cancellationToken);
        return Ok();
    }
}
