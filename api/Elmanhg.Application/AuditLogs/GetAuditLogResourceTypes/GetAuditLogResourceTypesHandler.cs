using Core.Auditing.Repositories;
using MediatR;

namespace Elmanhg.Application.AuditLogs.GetAuditLogResourceTypes;

public sealed class GetAuditLogResourceTypesHandler(IAuditLogRepository auditLogRepository) : IRequestHandler<GetAuditLogResourceTypesQuery, List<string>>
{
    public async Task<List<string>> Handle(GetAuditLogResourceTypesQuery request, CancellationToken cancellationToken)
    {
        return await auditLogRepository.GetResourceTypesAsync(cancellationToken).ConfigureAwait(false);
    }
}
