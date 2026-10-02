using Elmanhg.Application.Shared.RuntimeSettings;

namespace Elmanhg.Application.Configuration.Shared;

public sealed record RuntimeSettingGroupResult(RuntimeSettingGroup Group, List<RuntimeSettingResult> Settings);
