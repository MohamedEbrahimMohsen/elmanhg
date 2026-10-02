using Elmanhg.Application.Configuration.GetInfrastructureConfiguration;
using Elmanhg.Application.Configuration.GetRuntimeSettings;
using Elmanhg.Application.Configuration.ResetRuntimeSetting;
using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Configuration.UpdateRuntimeSetting;
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
}
