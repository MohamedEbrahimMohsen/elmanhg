using Core.OTP.Delivery;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.TeacherThreads;
using System.Diagnostics.Metrics;

namespace Elmanhg.Application.Shared.Observability;

public sealed class ElmanhgMetrics : IOtpDeliveryObserver
{
    public const string RequestTag = "elmanhg.request";
    public const string OutcomeTag = "elmanhg.outcome";
    public const string ChannelTag = "elmanhg.channel";
    public const string SourceTag = "elmanhg.source";
    public const string KindTag = "elmanhg.kind";
    public const string SuccessOutcome = "Success";
    public const string DeliveredOutcome = "Delivered";
    public const string FailedOutcome = "Failed";

    private readonly Counter<long> _requests;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _quizAnswers;
    private readonly Counter<long> _paymentNotifications;
    private readonly Counter<long> _otpSends;
    private readonly Counter<long> _clientErrors;
    private readonly Counter<long> _askTeacherSlaEvents;

    public ElmanhgMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ElmanhgTelemetry.SourceName);
        _requests = meter.CreateCounter<long>("elmanhg.requests", "{request}", "MediatR requests handled, by request type and outcome.");
        _requestDuration = meter.CreateHistogram<double>("elmanhg.request.duration", "s", "Time spent handling a MediatR request, by request type and outcome.");
        _quizAnswers = meter.CreateCounter<long>("elmanhg.quiz.answers", "{answer}", "Quiz answers submitted, by grading outcome.");
        _paymentNotifications = meter.CreateCounter<long>("elmanhg.payment.notifications", "{notification}", "Payment gateway notifications processed, by outcome.");
        _otpSends = meter.CreateCounter<long>("elmanhg.otp.sends", "{message}", "OTP messages sent, by channel and delivery outcome.");
        _clientErrors = meter.CreateCounter<long>("elmanhg.client.errors", "{error}", "Browser errors reported by the web app, by source.");
        _askTeacherSlaEvents = meter.CreateCounter<long>("elmanhg.ask_teacher.sla_events", "{event}", "Ask a Teacher SLA reminders and breaches recorded, by kind.");
    }

    public void RecordRequest(string requestName, string outcome, TimeSpan duration)
    {
        KeyValuePair<string, object?>[] tags = [new(RequestTag, requestName), new(OutcomeTag, outcome)];
        _requests.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);
    }

    public void RecordQuizAnswer(string outcome) => _quizAnswers.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome));

    public void RecordPaymentNotification(PaymentNotificationOutcome outcome) => _paymentNotifications.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome.ToString()));

    public void RecordOtpSend(OtpChannel channel, bool delivered) => _otpSends.Add(1, new KeyValuePair<string, object?>(ChannelTag, channel.ToString()), new KeyValuePair<string, object?>(OutcomeTag, delivered ? DeliveredOutcome : FailedOutcome));

    public void RecordClientError(ClientErrorSource source) => _clientErrors.Add(1, new KeyValuePair<string, object?>(SourceTag, source.ToString()));

    public void RecordAskTeacherSlaEvent(TeacherThreadSlaEventKind kind) => _askTeacherSlaEvents.Add(1, new KeyValuePair<string, object?>(KindTag, kind.ToString()));
}
