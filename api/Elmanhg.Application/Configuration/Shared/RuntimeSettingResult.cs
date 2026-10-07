using Core.Auditing;
using Core.Settings;
using Elmanhg.Application.Shared.RuntimeSettings;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elmanhg.Application.Configuration.Shared;

public sealed record RuntimeSettingResult(string Key, RuntimeSettingGroup Group, RuntimeSettingType Type, JsonElement Value, JsonElement DefaultValue, bool IsOverridden, decimal? Minimum, decimal? Maximum, List<string> AllowedValues, string LabelArabic, string LabelEnglish, string DescriptionArabic, string DescriptionEnglish, DateTimeOffset? UpdatedAt, [property: JsonIgnore] Guid? OverrideId) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => OverrideId;
}
