using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Exams.GetExpiredExamSessionIds;

public sealed class GetExpiredExamSessionIdsHandler(ISessionRepository sessionRepository, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider) : IRequestHandler<GetExpiredExamSessionIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetExpiredExamSessionIdsQuery request, CancellationToken cancellationToken)
    {
        var options = examsOptions.Value;
        var cutoff = timeProvider.GetUtcNow() - options.DeadlineGrace;
        var page = await sessionRepository.FindPaginatedAsync(1, options.AutoSubmitBatchSize, cancellationToken, filter: x => x.Kind != SessionKind.Quiz && x.SubmittedAt == null && x.Deadline != null && x.Deadline < cutoff && !request.ExcludedIds.Contains(x.Id), orderBy: query => query.OrderBy(x => x.Deadline), asNoTracking: true).ConfigureAwait(false);
        return page.Items
            .Select(x => x.Id)
            .ToList();
    }
}
