using System.Diagnostics;
using Core.Auditing.Entities;
using Core.Auditing.Repositories;
using Core.DDD.Identity;
using Core.Errors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Auditing;

/// <summary>
/// MediatR pipeline behavior that records one audit row per command implementing
/// <see cref="IAuditableCommand"/>. Commands that do not opt in pass through untouched.
/// Registered outermost (before other behaviors) so it observes the final outcome —
/// including validation failures and handler exceptions.
/// </summary>
public sealed class AuditBehaviour<TRequest, TResponse>(IAuditLogRepository auditLogRepository, IAuditChangeCollector auditChangeCollector, ICurrentUser currentUser, TimeProvider timeProvider, ILogger<AuditBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IAuditableCommand command)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var changesBefore = auditChangeCollector.Changes.Count;
        var outcome = AuditOutcome.Success;
        string? errorCode = null;
        var resourceId = command.AuditResourceId;

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            resourceId ??= (response as IAuditableResult)?.AuditResourceId;
            return response;
        }
        catch (Exception exception)
        {
            outcome = AuditOutcome.Failure;
            errorCode = (exception as BaseException)?.ErrorCode ?? exception.GetType().Name;
            throw;
        }
        finally
        {
            var entry = AuditLog.Create(
                timestamp: timeProvider.GetUtcNow(),
                actorUserId: currentUser.UserId,
                actorUserName: currentUser.UserName,
                actorRole: currentUser.Role,
                action: command.AuditAction,
                resourceType: command.AuditResourceType,
                resourceId,
                outcome: outcome.ToString(),
                errorCode: errorCode,
                traceId: Activity.Current?.TraceId.ToString(),
                diff: AuditDiff.Serialize(auditChangeCollector.Changes.Skip(changesBefore).ToList())
            );

            // Audit persistence must never break the request it describes.
            try
            {
                // The request token may already be cancelled after the handler committed; the row must still be written.
                await auditLogRepository.AppendAsync(entry, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Failed to persist audit entry for action {entry.Action ?? string.Empty} on {entry.ResourceType ?? string.Empty} {entry.ResourceId} (TraceId {entry.TraceId ?? string.Empty})");
            }
        }
    }
}
