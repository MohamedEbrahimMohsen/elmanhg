namespace Core.Settings;

public interface IRuntimeSettingOverrideStore
{
    Task<IReadOnlyList<IRuntimeSettingOverride>> GetOverridesAsync(CancellationToken cancellationToken);
}
