namespace Elmanhg.Application.Shared.RuntimeSettings;

public interface IRuntimeSettingDefinitions
{
    IReadOnlyList<RuntimeSettingDefinition> Definitions { get; }

    IReadOnlyList<RuntimeSettingConstraint> Constraints => [];
}
