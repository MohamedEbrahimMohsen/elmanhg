namespace Core.Settings;

public interface IRuntimeSettingDefinitions
{
    IReadOnlyList<RuntimeSettingDefinition> Definitions { get; }

    IReadOnlyList<RuntimeSettingConstraint> Constraints => [];
}
