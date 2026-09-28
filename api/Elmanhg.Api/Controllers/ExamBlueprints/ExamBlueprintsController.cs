using Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;
using Elmanhg.Application.ExamBlueprints.GetSubjectExamBlueprints;
using Elmanhg.Application.ExamBlueprints.SaveSubjectExamBlueprint;
using Elmanhg.Application.ExamBlueprints.SaveUnitExamBlueprint;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.ExamBlueprints;

[ApiController]
[Route("api/exam-blueprints")]
[Authorize]
public class ExamBlueprintsController(IMediator mediator) : ControllerBase
{
    [HttpGet("subjects/{subjectId:guid}", Name = "GetSubjectExamBlueprints")]
    [Authorize(Policy = DefaultCodes.BlueprintsManage)]
    [ProducesResponseType<SubjectExamBlueprintsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubjectExamBlueprints([FromRoute] Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectExamBlueprintsQuery(subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("subjects/{subjectId:guid}", Name = "SaveSubjectExamBlueprint")]
    [Authorize(Policy = DefaultCodes.BlueprintsManage)]
    [ProducesResponseType<ExamBlueprintResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveSubjectExamBlueprint([FromRoute] Guid subjectId, [FromBody] ExamBlueprintInput request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveSubjectExamBlueprintCommand(subjectId, request), cancellationToken);
        return Ok(result);
    }

    [HttpPut("units/{unitId:guid}", Name = "SaveUnitExamBlueprint")]
    [Authorize(Policy = DefaultCodes.BlueprintsManage)]
    [ProducesResponseType<ExamBlueprintResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveUnitExamBlueprint([FromRoute] Guid unitId, [FromBody] ExamBlueprintInput request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveUnitExamBlueprintCommand(unitId, request), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{examBlueprintId:guid}", Name = "DeleteExamBlueprint")]
    [Authorize(Policy = DefaultCodes.BlueprintsManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteExamBlueprint([FromRoute] Guid examBlueprintId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteExamBlueprintCommand(examBlueprintId), cancellationToken);
        return Ok();
    }
}
