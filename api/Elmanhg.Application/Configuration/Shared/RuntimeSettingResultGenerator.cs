using Core.Settings;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;

namespace Elmanhg.Application.Configuration.Shared;

public static class RuntimeSettingResultGenerator
{
    public static RuntimeSettingResult Generate(RuntimeSettingDefinition definition, RuntimeSettingValues values, RuntimeSettingOverride? row) => new(definition.Key, Enum.Parse<RuntimeSettingGroup>(definition.Group), definition.Type, values.Raw(definition.Key), definition.DefaultValue, row?.Value is not null, definition.Minimum, definition.Maximum, [.. definition.AllowedValues], definition.Label.Arabic, definition.Label.English, definition.Description.Arabic, definition.Description.English, row?.Value is null ? null : row.UpdationDate, row?.Id);
}
