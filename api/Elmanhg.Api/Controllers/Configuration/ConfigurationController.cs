using Elmanhg.Application.Configuration.GetInfrastructureConfiguration;
using Elmanhg.Application.Configuration.GetRuntimeSettings;
using Elmanhg.Application.Configuration.ResetRuntimeSetting;
using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Configuration.UpdateRuntimeSetting;
using Elmanhg.Application.SlaCalendars.CreateExamPeriod;
using Elmanhg.Application.SlaCalendars.DeleteExamPeriod;
using Elmanhg.Application.SlaCalendars.GetExamPeriods;
using Elmanhg.Application.SlaCalendars.Shared;
using Elmanhg.Application.SlaCalendars.UpdateExamPeriod;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Configuration;

[ApiController]
[Route("api/configuration")]
[Authorize]
public class ConfigurationController(IMediator mediator) : ControllerBase
{
    [HttpGet("settings", Name = "GetRuntimeSettings")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<List<RuntimeSettingGroupResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetRuntimeSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRuntimeSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("settings/{key}", Name = "UpdateRuntimeSetting")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<RuntimeSettingResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateRuntimeSetting([FromRoute] string key, [FromBody] UpdateRuntimeSettingRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateRuntimeSettingCommand(key, request.Value), cancellationToken);
        return Ok(result);
    }

    [HttpPost("settings/{key}/reset", Name = "ResetRuntimeSetting")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<RuntimeSettingResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResetRuntimeSetting([FromRoute] string key, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResetRuntimeSettingCommand(key), cancellationToken);
        return Ok(result);
    }

    [HttpGet("infrastructure", Name = "GetInfrastructureConfiguration")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<InfrastructureConfigurationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetInfrastructureConfiguration(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInfrastructureConfigurationQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("exam-periods", Name = "GetExamPeriods")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<List<ExamPeriodResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetExamPeriods(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExamPeriodsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("exam-periods", Name = "CreateExamPeriod")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<ExamPeriodResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateExamPeriod([FromBody] CreateExamPeriodCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("exam-periods/{examPeriodId:guid}", Name = "UpdateExamPeriod")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType<ExamPeriodResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateExamPeriod([FromRoute] Guid examPeriodId, [FromBody] UpdateExamPeriodRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateExamPeriodCommand(examPeriodId, request.Name, request.StartDate, request.EndDate), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("exam-periods/{examPeriodId:guid}", Name = "DeleteExamPeriod")]
    [Authorize(Policy = DefaultCodes.ConfigurationManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteExamPeriod([FromRoute] Guid examPeriodId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteExamPeriodCommand(examPeriodId), cancellationToken);
        return Ok();
    }
}
