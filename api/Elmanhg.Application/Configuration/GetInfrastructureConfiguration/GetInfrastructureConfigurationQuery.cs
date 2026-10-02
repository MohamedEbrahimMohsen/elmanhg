using Elmanhg.Application.Configuration.Shared;
using MediatR;

namespace Elmanhg.Application.Configuration.GetInfrastructureConfiguration;

public sealed record GetInfrastructureConfigurationQuery : IRequest<InfrastructureConfigurationResult>;
