using Core.Errors;

namespace Elmanhg.Application.Shared.RuntimeSettings;

public sealed class RuntimeSettingRegistry
{
    private readonly Dictionary<string, RuntimeSettingDefinition> definitionsByKey;

    public RuntimeSettingRegistry(IEnumerable<IRuntimeSettingDefinitions> groups)
    {
        var list = groups.ToList();
        var problems = FindProblems(list);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", problems));
        }

        Definitions = list
            .SelectMany(x => x.Definitions)
            .OrderBy(x => x.Group)
            .ToList();
        Constraints = list
            .SelectMany(x => x.Constraints)
            .ToList();
        definitionsByKey = Definitions.ToDictionary(x => x.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<RuntimeSettingDefinition> Definitions { get; }

    public IReadOnlyList<RuntimeSettingConstraint> Constraints { get; }

    public RuntimeSettingDefinition? Find(string key) => definitionsByKey.GetValueOrDefault(key);

    public void EnsureConstraintsHold(RuntimeSettingValues values)
    {
        var broken = Constraints.FirstOrDefault(x => !x.IsSatisfiedBy(values));
        if (broken is not null)
        {
            throw new BusinessRuleViolationCoreException(broken.ErrorCode);
        }
    }

    public static List<string> FindProblems(IReadOnlyList<IRuntimeSettingDefinitions> groups)
    {
        var definitions = groups
            .SelectMany(x => x.Definitions)
            .ToList();
        var duplicates = definitions
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => $"Runtime setting {x.Key} is registered more than once.");
        var invalidDefaults = definitions
            .Where(x => !RuntimeSettingValueRules.IsValid(x, x.DefaultValue))
            .Select(x => $"Runtime setting {x.Key}: the configured default {x.DefaultValue.GetRawText()} is outside its allowed values.");
        return [.. duplicates, .. invalidDefaults];
    }
}
