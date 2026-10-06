using System.Diagnostics.Metrics;

namespace Core.Observability;

public sealed class RequestMetrics
{
    public const string SuccessOutcome = "Success";

    private readonly Counter<long> _requests;
    private readonly Histogram<double> _requestDuration;

    public RequestMetrics(IMeterFactory meterFactory, string meterName, string metricPrefix)
    {
        var meter = meterFactory.Create(meterName);
        RequestTag = $"{metricPrefix}.request";
        OutcomeTag = $"{metricPrefix}.outcome";
        _requests = meter.CreateCounter<long>($"{metricPrefix}.requests", "{request}", "MediatR requests handled, by request type and outcome.");
        _requestDuration = meter.CreateHistogram<double>($"{metricPrefix}.request.duration", "s", "Time spent handling a MediatR request, by request type and outcome.");
    }

    public string RequestTag { get; }

    public string OutcomeTag { get; }

    public void RecordRequest(string requestName, string outcome, TimeSpan duration)
    {
        KeyValuePair<string, object?>[] tags = [new(RequestTag, requestName), new(OutcomeTag, outcome)];
        _requests.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);
    }
}
