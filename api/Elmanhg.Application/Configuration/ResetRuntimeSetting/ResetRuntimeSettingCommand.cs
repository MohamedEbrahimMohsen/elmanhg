using Core.Auditing;
using Elmanhg.Application.Configuration.Shared;
using MediatR;

namespace Elmanhg.Application.Configuration.ResetRuntimeSetting;

public sealed record ResetRuntimeSettingCommand(string Key) : IRequest<RuntimeSettingResult>, IAuditableCommand
{
    public string AuditAction => "RuntimeSetting.Reset";
    public string AuditResourceType => "RuntimeSetting";
    public Guid? AuditResourceId => null;
}
