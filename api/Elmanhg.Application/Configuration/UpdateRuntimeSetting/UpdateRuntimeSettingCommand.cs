using Core.Auditing;
using Elmanhg.Application.Configuration.Shared;
using MediatR;
using System.Text.Json;

namespace Elmanhg.Application.Configuration.UpdateRuntimeSetting;

public sealed record UpdateRuntimeSettingCommand(string Key, JsonElement Value) : IRequest<RuntimeSettingResult>, IAuditableCommand
{
    public string AuditAction => "RuntimeSetting.Update";
    public string AuditResourceType => "RuntimeSetting";
    public Guid? AuditResourceId => null;
}
