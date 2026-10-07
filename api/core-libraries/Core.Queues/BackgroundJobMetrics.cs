using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Core.Queues;

public sealed class BackgroundJobMetrics
{
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();
    private readonly TimeProvider _timeProvider;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _runs;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _items;

    public BackgroundJobMetrics(IMeterFactory meterFactory, TimeProvider timeProvider, string meterName, string metricPrefix, ActivitySource activitySource)
    {
        _timeProvider = timeProvider;
        _activitySource = activitySource;
        JobTag = $"{metricPrefix}.job";
        OutcomeTag = $"{metricPrefix}.outcome";
        var meter = meterFactory.Create(meterName);
        _runs = meter.CreateCounter<long>($"{metricPrefix}.job.runs", "{run}", "Background job sweeps completed, by job and outcome.");
        _duration = meter.CreateHistogram<double>($"{metricPrefix}.job.duration", "s", "Time one background job sweep took, by job and outcome.");
        _items = meter.CreateCounter<long>($"{metricPrefix}.job.items", "{item}", "Items a background job sweep processed, by job and item outcome.");
        meter.CreateObservableGauge($"{metricPrefix}.job.last_success", () => _jobs.Select(x => new Measurement<long>(Interlocked.Read(ref x.Value.LastSuccessUnixSeconds), new KeyValuePair<string, object?>(JobTag, x.Key))), "s", "Unix time of the last sweep that listed its work, per job.");
        meter.CreateObservableGauge($"{metricPrefix}.job.interval", () => _jobs.Select(x => new Measurement<double>(x.Value.IntervalSeconds, new KeyValuePair<string, object?>(JobTag, x.Key))), "s", "Configured sweep interval, per job.");
    }

    public string JobTag { get; }

    public string OutcomeTag { get; }

    public void Register(string jobName, TimeSpan interval) => _jobs[jobName] = new JobState { IntervalSeconds = interval.TotalSeconds, LastSuccessUnixSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds() };

    public BackgroundJobRun StartRun(string jobName)
    {
        var activity = _activitySource.StartActivity($"job {jobName}");
        activity?.SetTag(JobTag, jobName);
        return new BackgroundJobRun(this, jobName, Stopwatch.GetTimestamp(), activity);
    }

    internal void Complete(BackgroundJobRun run)
    {
        var outcome = run.Outcome.ToString();
        var job = new KeyValuePair<string, object?>(JobTag, run.JobName);
        _runs.Add(1, job, new KeyValuePair<string, object?>(OutcomeTag, outcome));
        _duration.Record(Stopwatch.GetElapsedTime(run.StartedTimestamp).TotalSeconds, job, new KeyValuePair<string, object?>(OutcomeTag, outcome));
        RecordItems(job, BackgroundJobRunOutcome.Succeeded, run.SucceededItems);
        RecordItems(job, BackgroundJobRunOutcome.Failed, run.FailedItems);
        if (run.Outcome != BackgroundJobRunOutcome.Failed && _jobs.TryGetValue(run.JobName, out var state))
        {
            Interlocked.Exchange(ref state.LastSuccessUnixSeconds, _timeProvider.GetUtcNow().ToUnixTimeSeconds());
        }

        run.Activity?.SetTag(OutcomeTag, outcome);
        if (run.Outcome == BackgroundJobRunOutcome.Failed)
        {
            run.Activity?.SetStatus(ActivityStatusCode.Error);
        }

        run.Activity?.Dispose();
    }

    private void RecordItems(KeyValuePair<string, object?> job, BackgroundJobRunOutcome itemOutcome, int count)
    {
        if (count > 0)
        {
            _items.Add(count, job, new KeyValuePair<string, object?>(OutcomeTag, itemOutcome.ToString()));
        }
    }

    private sealed class JobState
    {
        public long LastSuccessUnixSeconds;

        public double IntervalSeconds { get; init; }
    }
}
