using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Domain.Analytics;
using MediatR;

namespace Elmanhg.Application.Analytics.RecordFunnelEvent;

public sealed class RecordFunnelEventHandler(IFunnelEventRepository funnelEventRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<RecordFunnelEventCommand>
{
    public async Task Handle(RecordFunnelEventCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId == null || currentUserService.UserId == default ? null : currentUserService.UserId;
        await funnelEventRepository.AddAsync(FunnelEvent.Record(request.AnonymousId, userId, request.Type, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);

        await funnelEventRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
