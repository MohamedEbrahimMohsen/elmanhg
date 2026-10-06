using Core.Auditing.Repositories;
using Core.DDD.Models;
using Elmanhg.Application.AuditLogs.Shared;
using MediatR;

namespace Elmanhg.Application.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsHandler(IAuditLogRepository auditLogRepository) : IRequestHandler<GetAuditLogsQuery, PageData<AuditLogResult>>
{
    public async Task<PageData<AuditLogResult>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var page = await auditLogRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetAuditLogsFilter.Build(request), orderBy: query => query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);

        return page.Map(AuditLogResultGenerator.Generate);
    }
}
