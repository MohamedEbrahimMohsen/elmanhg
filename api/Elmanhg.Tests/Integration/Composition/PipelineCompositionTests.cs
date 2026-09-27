using Core.Auditing;
using Core.CQRS.Behaviours;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Composition;

public sealed record PipelineProbeRequest : IRequest<Unit>;

public sealed class PipelineCompositionTests(ApiFactory factory)
{
    [Fact]
    public void Resolve_PipelineBehaviours_IncludesValidationBehaviour()
    {
        using var scope = factory.Services.CreateScope();

        var behaviours = scope.ServiceProvider.GetServices<IPipelineBehavior<PipelineProbeRequest, Unit>>();

        behaviours.Should().ContainItemsAssignableTo<ValidationBehaviour<PipelineProbeRequest, Unit>>();
    }

    [Fact]
    public void Resolve_PipelineBehaviours_AuditBehaviourIsOutermost()
    {
        using var scope = factory.Services.CreateScope();

        var behaviours = scope.ServiceProvider.GetServices<IPipelineBehavior<PipelineProbeRequest, Unit>>();

        behaviours.First().Should().BeOfType<AuditBehaviour<PipelineProbeRequest, Unit>>();
    }
}
