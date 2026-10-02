namespace Elmanhg.Application.Shared.RuntimeSettings;

public interface IRuntimeSettings
{
    Task<T> GetAsync<T>(RuntimeSettingKey<T> key, CancellationToken cancellationToken);

    Task<RuntimeSettingValues> GetValuesAsync(CancellationToken cancellationToken);
}
