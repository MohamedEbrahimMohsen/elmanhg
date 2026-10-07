using Core.Errors;

namespace Core.Settings;

public sealed class RuntimeSettingRegistry
{
    private readonly Dictionary<string, RuntimeSettingDefinition> definitionsByKey;

    public RuntimeSettingRegistry(IEnumerable<IRuntimeSettingDefinitions> groups, IReadOnlyList<string> groupOrder)
    {
        var list = groups.ToList();
        var problems = FindProblems(list, groupOrder);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", problems));
        }

        var positions = groupOrder.ToList();
        Definitions = list
            .SelectMany(x => x.Definitions)
            .OrderBy(x => positions.IndexOf(x.Group))
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

    public static List<string> FindProblems(IReadOnlyList<IRuntimeSettingDefinitions> groups, IReadOnlyList<string> groupOrder)
    {
        var definitions = groups
            .SelectMany(x => x.Definitions)
            .ToList();
        var duplicates = definitions
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => $"Runtime setting {x.Key} is registered more than once.");
        var unknownGroups = definitions
            .Where(x => !groupOrder.Contains(x.Group, StringComparer.Ordinal))
            .Select(x => $"Runtime setting {x.Key}: group {x.Group} is not in the group order.");
        var invalidDefaults = definitions
            .Where(x => !RuntimeSettingValueRules.IsValid(x, x.DefaultValue))
            .Select(x => $"Runtime setting {x.Key}: the configured default {x.DefaultValue.GetRawText()} is outside its allowed values.");
        return [.. duplicates, .. unknownGroups, .. invalidDefaults];
    }
}
