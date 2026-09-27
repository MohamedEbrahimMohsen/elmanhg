using MediatR;

namespace Elmanhg.Application.AuditLogs.GetAuditLogResourceTypes;

public sealed record GetAuditLogResourceTypesQuery : IRequest<List<string>>;
