using System.ComponentModel.DataAnnotations;

namespace Core.Settings;

public sealed class RuntimeSettingsOptions
{
    public const string SectionName = "RuntimeSettings";

    [Range(1, 3600)]
    public int CacheSeconds { get; set; } = 30;
}
