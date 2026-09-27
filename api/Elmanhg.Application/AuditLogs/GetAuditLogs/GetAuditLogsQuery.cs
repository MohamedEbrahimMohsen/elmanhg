using Core.DDD.Models;
using Elmanhg.Application.AuditLogs.Shared;
using MediatR;

namespace Elmanhg.Application.AuditLogs.GetAuditLogs;

public sealed record GetAuditLogsQuery(string? Actor, string? ResourceType, DateTimeOffset? From, DateTimeOffset? To, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<AuditLogResult>>;
