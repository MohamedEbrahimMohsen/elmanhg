using Core.Queues;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Queues;

internal sealed class ProbeSweepWorker(IServiceScopeFactory scopeFactory, SweepOptions options, TimeProvider timeProvider, ILogger logger, BackgroundJobMetrics jobMetrics) : SweepWorker<SweepOptions>(scopeFactory, Options.Create(options), timeProvider, logger, jobMetrics)
{
    public const string Name = "probe-job";

    public Func<IReadOnlyCollection<Guid>, Task<List<Guid>>> List { get; set; } = _ => Task.FromResult<List<Guid>>([]);
    public Func<Guid, Task> Process { get; set; } = _ => Task.CompletedTask;
    public Func<Guid, string, Task>? RecordFailure { get; set; }
    public Func<Task>? BeforeList { get; set; }

    protected override string JobName => Name;

    protected override SweepOptions SweepOptionsOf(SweepOptions options) => options;

    protected override Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => List(deferredIds);

    protected override Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => Process(id);

    protected override Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => RecordFailure is null ? base.RecordFailureAsync(sender, id, errorCode, cancellationToken) : RecordFailure(id, errorCode);

    protected override Task BeforeListAsync(SweepOptions sweep, CancellationToken cancellationToken) => BeforeList is null ? base.BeforeListAsync(sweep, cancellationToken) : BeforeList();
}
