using Microsoft.Extensions.Options;

namespace Core.Settings;

public sealed class RuntimeSettingsOptionsValidator(IEnumerable<IRuntimeSettingDefinitions> groups, IReadOnlyList<string> groupOrder) : IValidateOptions<RuntimeSettingsOptions>
{
    public ValidateOptionsResult Validate(string? name, RuntimeSettingsOptions options)
    {
        var problems = RuntimeSettingRegistry.FindProblems(groups.ToList(), groupOrder);
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
