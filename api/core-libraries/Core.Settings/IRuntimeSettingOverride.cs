namespace Core.Settings;

public interface IRuntimeSettingOverride
{
    string Key { get; }

    string? Value { get; }
}
