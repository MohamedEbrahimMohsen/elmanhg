using Core.Auditing;
using Core.CQRS.Behaviours;
using Core.Observability;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
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

    [Fact]
    public void Resolve_PipelineBehaviours_RequestMetricsRunsInsideAuditAndOutsideValidation()
    {
        using var scope = factory.Services.CreateScope();

        var behaviours = scope.ServiceProvider.GetServices<IPipelineBehavior<PipelineProbeRequest, Unit>>().ToList();

        var metricsIndex = behaviours.FindIndex(x => x is RequestMetricsBehaviour<PipelineProbeRequest, Unit>);
        metricsIndex.Should().Be(1);
        metricsIndex.Should().BeLessThan(behaviours.FindIndex(x => x is ValidationBehaviour<PipelineProbeRequest, Unit>));
    }

    [Fact]
    public void Resolve_SubmitAnswerPipeline_IncludesQuizAnswerMetricsBehaviour()
    {
        using var scope = factory.Services.CreateScope();

        var behaviours = scope.ServiceProvider.GetServices<IPipelineBehavior<SubmitAnswerCommand, SessionItemResult>>();

        behaviours.Should().ContainItemsAssignableTo<QuizAnswerMetricsBehaviour>();
    }

    [Fact]
    public void Resolve_PaymentNotificationPipeline_IncludesPaymentNotificationMetricsBehaviour()
    {
        using var scope = factory.Services.CreateScope();

        var behaviours = scope.ServiceProvider.GetServices<IPipelineBehavior<ProcessPaymentNotificationCommand, PaymentNotificationResult>>();

        behaviours.Should().ContainItemsAssignableTo<PaymentNotificationMetricsBehaviour>();
    }
}
