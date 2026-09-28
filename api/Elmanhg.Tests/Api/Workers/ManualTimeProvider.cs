namespace Elmanhg.Tests.Api.Workers;

public sealed class ManualTimeProvider : TimeProvider
{
    private TimerCallback? _callback;
    private object? _state;
    private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task TimerCreated => _timerCreated.Task;

    public void Tick() => _callback!(_state);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _callback = callback;
        _state = state;
        _timerCreated.TrySetResult();
        return new ManualTimer();
    }

    private sealed class ManualTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
