namespace Core.Queues;

public class SweepOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);
}
