namespace Elmanhg.Application.Configuration.Shared;

public interface IInfrastructureConfigurationReader
{
    Task<InfrastructureConfigurationResult> ReadAsync(CancellationToken cancellationToken);
}
