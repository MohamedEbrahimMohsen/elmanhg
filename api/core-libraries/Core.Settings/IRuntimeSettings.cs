namespace Core.Settings;

public interface IRuntimeSettings
{
    Task<T> GetAsync<T>(RuntimeSettingKey<T> key, CancellationToken cancellationToken);

    Task<RuntimeSettingValues> GetValuesAsync(CancellationToken cancellationToken);
}
