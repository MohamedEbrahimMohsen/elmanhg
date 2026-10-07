using Core.DDD.Models;
using Core.Settings;

namespace Elmanhg.Tests.Core.Settings;

public sealed record ProbeOverride(string Key, string? Value) : IRuntimeSettingOverride;

public sealed class ProbeOverrideStore : IRuntimeSettingOverrideStore
{
    public Task<IReadOnlyList<IRuntimeSettingOverride>> GetOverridesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IRuntimeSettingOverride>>([]);
}

public sealed class ProbeRuntimeSettings(int countDefault = 5, string countGroup = ProbeRuntimeSettings.LimitsGroup) : IRuntimeSettingDefinitions
{
    public const string FlagsGroup = "Flags";
    public const string LimitsGroup = "Limits";
    public const string CountAboveCeilingCode = "PROBE_COUNT_ABOVE_CEILING";
    public static readonly IReadOnlyList<string> GroupOrder = [LimitsGroup, FlagsGroup];
    public static readonly RuntimeSettingKey<int> Count = new("probe.count");
    public static readonly RuntimeSettingKey<bool> Enabled = new("probe.enabled");
    public static readonly RuntimeSettingKey<int> Ceiling = new("probe.ceiling");
    private static readonly LocalizedText Text = new("نص", "Text");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForInteger(Count, countGroup, countDefault, 1, 10, Text, Text),
        RuntimeSettingDefinition.ForBoolean(Enabled, FlagsGroup, false, Text, Text),
        RuntimeSettingDefinition.ForInteger(Ceiling, LimitsGroup, 8, 1, 10, Text, Text),
    ];

    public IReadOnlyList<RuntimeSettingConstraint> Constraints => [new(CountAboveCeilingCode, values => values.Get(Count) < values.Get(Ceiling))];
}
