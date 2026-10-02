using Elmanhg.Application.Configuration.Shared;
using MediatR;

namespace Elmanhg.Application.Configuration.GetInfrastructureConfiguration;

public sealed class GetInfrastructureConfigurationHandler(IInfrastructureConfigurationReader infrastructureConfigurationReader) : IRequestHandler<GetInfrastructureConfigurationQuery, InfrastructureConfigurationResult>
{
    public async Task<InfrastructureConfigurationResult> Handle(GetInfrastructureConfigurationQuery request, CancellationToken cancellationToken)
    {
        return await infrastructureConfigurationReader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }
}
