using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class PaymentNotificationMetricsBehaviourTests : IDisposable
{
    private static readonly ProcessPaymentNotificationCommand Command = new("{}", "signature");
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly MetricCollector<long> _notifications;
    private readonly PaymentNotificationMetricsBehaviour _behaviour;

    public PaymentNotificationMetricsBehaviourTests()
    {
        _notifications = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.payment.notifications");
        _behaviour = new PaymentNotificationMetricsBehaviour(new ElmanhgMetrics(_meterFactory));
    }

    [Fact]
    public async Task Handle_NotificationProcessed_RecordsOutcome()
    {
        var processed = new PaymentNotificationResult(Guid.CreateVersion7(), PaymentNotificationOutcome.Succeeded);

        var result = await _behaviour.Handle(Command, _ => Task.FromResult(processed), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(processed);
        _notifications.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.OutcomeTag, "Succeeded");
    }

    [Fact]
    public async Task Handle_NextThrows_RecordsNothing()
    {
        var act = () => _behaviour.Handle(Command, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _notifications.GetMeasurementSnapshot().Should().BeEmpty();
    }

    public void Dispose() => _notifications.Dispose();
}
