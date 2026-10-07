using System.Diagnostics;

namespace Core.Queues;

public sealed class BackgroundJobRun : IDisposable
{
    private readonly BackgroundJobMetrics _metrics;
    private bool _listingFailed;
    private bool _completed;

    internal BackgroundJobRun(BackgroundJobMetrics metrics, string jobName, long startedTimestamp, Activity? activity)
    {
        _metrics = metrics;
        JobName = jobName;
        StartedTimestamp = startedTimestamp;
        Activity = activity;
    }

    public string JobName { get; }

    public int SucceededItems { get; private set; }

    public int FailedItems { get; private set; }

    internal long StartedTimestamp { get; }

    internal Activity? Activity { get; }

    public BackgroundJobRunOutcome Outcome => _listingFailed ? BackgroundJobRunOutcome.Failed : FailedItems > 0 ? BackgroundJobRunOutcome.PartiallyFailed : BackgroundJobRunOutcome.Succeeded;

    public void MarkListingFailed() => _listingFailed = true;

    public void ItemSucceeded() => SucceededItems++;

    public void ItemFailed() => FailedItems++;

    public void Dispose()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _metrics.Complete(this);
    }
}

public enum BackgroundJobRunOutcome { Succeeded, PartiallyFailed, Failed }
