using Elmanhg.Application.Shared.RuntimeSettings;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.Options;

public sealed class RuntimeSettingsOptionsValidator(IEnumerable<IRuntimeSettingDefinitions> groups) : IValidateOptions<RuntimeSettingsOptions>
{
    public ValidateOptionsResult Validate(string? name, RuntimeSettingsOptions options)
    {
        var problems = RuntimeSettingRegistry.FindProblems(groups.ToList());
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
