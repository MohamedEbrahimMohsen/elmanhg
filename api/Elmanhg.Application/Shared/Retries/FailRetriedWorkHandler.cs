using Core.DDD.Entities;
using Core.DDD.Repositories;
using Elmanhg.Domain.SharedKernel;
using MediatR;

namespace Elmanhg.Application.Shared.Retries;

public abstract class FailRetriedWorkHandler<TCommand, TWork>(IRepository<TWork> repository, TimeProvider timeProvider) : IRequestHandler<TCommand> where TCommand : IFailRetriedWorkCommand where TWork : class, IEntity, IRetriedWork
{
    protected abstract int MaxAttempts { get; }
    protected abstract TimeSpan RetryBaseDelay { get; }

    public async Task Handle(TCommand request, CancellationToken cancellationToken)
    {
        var work = await repository.FirstOrDefaultAsync(x => x.Id == request.WorkId, cancellationToken).ConfigureAwait(false);
        if (work is null || !work.IsPending)
        {
            return;
        }

        work.FailAttempt(request.ErrorCode, timeProvider.GetUtcNow(), MaxAttempts, RetryBaseDelay);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
