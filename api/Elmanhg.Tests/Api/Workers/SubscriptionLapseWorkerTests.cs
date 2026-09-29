using Elmanhg.Api.Workers;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetLapsedSubscriptionIds;
using Elmanhg.Application.Subscriptions.LapseSubscription;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Api.Workers;

public sealed class SubscriptionLapseWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<SubscriptionLapseWorker> _logger = Substitute.For<ILogger<SubscriptionLapseWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_FailingSubscriptions_LogsAndLapsesTheRest()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetLapsedSubscriptionIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        FailLapse(ids[0], new InvalidOperationException());
        FailLapse(ids[1], new OperationCanceledException());
        _sender.Send(Arg.Is<LapseSubscriptionCommand>(x => x.SubscriptionId == ids[2]), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.CompletedTask;
        });

        using var worker = await RunAsync(new SubscriptionsOptions());

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning, LogLevel.Warning);
        await _sender.Received(1).Send(Arg.Is<LapseSubscriptionCommand>(x => x.SubscriptionId == ids[2]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_FailedIds_AreSkippedUntilTheBacklogEnds()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<List<Guid>> excluded = [];
        Queue<List<Guid>> pages = new([[ids[0], ids[1]], [ids[2], ids[3]], [ids[4]], []]);
        _sender.Send(Arg.Any<GetLapsedSubscriptionIdsQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            excluded.Add([.. call.Arg<GetLapsedSubscriptionIdsQuery>().ExcludedIds]);
            if (excluded.Count < 4)
            {
                _time.Tick();
            }
            else
            {
                Complete();
            }

            return Task.FromResult(pages.Dequeue());
        });
        _sender.Send(Arg.Any<LapseSubscriptionCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        _sender.Send(Arg.Is<LapseSubscriptionCommand>(x => x.SubscriptionId == ids[4]), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        using var worker = await RunAsync(new SubscriptionsOptions { LapseSweepBatchSize = 2 });

        excluded[0].Should().BeEmpty();
        excluded[1].Should().BeEquivalentTo([ids[0], ids[1]]);
        excluded[2].Should().BeEquivalentTo([ids[0], ids[1], ids[2], ids[3]]);
        excluded[3].Should().BeEmpty();
        await _sender.Received(1).Send(Arg.Is<LapseSubscriptionCommand>(x => x.SubscriptionId == ids[4]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_DuringSweep_EndsTheLoopWithoutLogging()
    {
        _sender.Send(Arg.Any<GetLapsedSubscriptionIdsQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Complete();
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return new List<Guid>();
        });
        using var worker = await RunAsync(new SubscriptionsOptions());

        await worker.StopAsync(TestContext.Current.CancellationToken);

        worker.ExecuteTask!.IsCompleted.Should().BeTrue();
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_Disabled_EndsWithoutSweeping()
    {
        using var worker = new SubscriptionLapseWorker(ScopeFactory(), Options.Create(new SubscriptionsOptions { LapseSweepEnabled = false }), _time, _logger);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        _sender.ReceivedCalls().Should().BeEmpty();
    }

    private async Task<SubscriptionLapseWorker> RunAsync(SubscriptionsOptions options)
    {
        var worker = new SubscriptionLapseWorker(ScopeFactory(), Options.Create(options), _time, _logger);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void FailLapse(Guid subscriptionId, Exception exception) => _sender.Send(Arg.Is<LapseSubscriptionCommand>(x => x.SubscriptionId == subscriptionId), Arg.Any<CancellationToken>()).Returns(Task.FromException(exception));

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
