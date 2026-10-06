using Core.OTP.Delivery;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class ElmanhgMetricsTests
{
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly ElmanhgMetrics _metrics;

    public ElmanhgMetricsTests() => _metrics = new ElmanhgMetrics(_meterFactory);

    [Fact]
    public void RecordQuizAnswer_Correct_IncrementsWithOutcomeTag()
    {
        using var answers = Collector<long>("elmanhg.quiz.answers");

        _metrics.RecordQuizAnswer("Correct");

        var answer = answers.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        answer.Value.Should().Be(1);
        answer.Tags.Should().Contain(ElmanhgMetrics.OutcomeTag, "Correct");
    }

    [Fact]
    public void RecordPaymentNotification_FlaggedForReview_TagsEnumName()
    {
        using var notifications = Collector<long>("elmanhg.payment.notifications");

        _metrics.RecordPaymentNotification(PaymentNotificationOutcome.FlaggedForReview);

        notifications.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.OutcomeTag, "FlaggedForReview");
    }

    [Theory]
    [InlineData(true, "Delivered")]
    [InlineData(false, "Failed")]
    public void RecordOtpSend_Delivery_TagsChannelAndOutcome(bool delivered, string expectedOutcome)
    {
        using var sends = Collector<long>("elmanhg.otp.sends");

        _metrics.RecordOtpSend(OtpChannel.WhatsApp, delivered);

        sends.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.ChannelTag, "WhatsApp").And.Contain(ElmanhgMetrics.OutcomeTag, expectedOutcome);
    }

    [Fact]
    public void RecordClientError_Route_TagsSource()
    {
        using var errors = Collector<long>("elmanhg.client.errors");

        _metrics.RecordClientError(ClientErrorSource.Route);

        errors.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.SourceTag, "Route");
    }

    [Fact]
    public void RecordAskTeacherSlaEvent_Breach_TagsKind()
    {
        using var events = Collector<long>("elmanhg.ask_teacher.sla_events");

        _metrics.RecordAskTeacherSlaEvent(TeacherThreadSlaEventKind.Breach);

        var measurement = events.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        measurement.Value.Should().Be(1);
        measurement.Tags.Should().Contain(ElmanhgMetrics.KindTag, "Breach");
    }

    private MetricCollector<T> Collector<T>(string instrument) where T : struct => new(_meterFactory, ElmanhgTelemetry.SourceName, instrument);
}
