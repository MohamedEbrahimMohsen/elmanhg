using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Shared.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Application.Observability.ReportClientError;

public sealed class ReportClientErrorHandler(ElmanhgMetrics metrics, ICurrentUserService currentUserService, ILogger<ReportClientErrorHandler> logger) : IRequestHandler<ReportClientErrorCommand>
{
    public Task Handle(ReportClientErrorCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId == null || currentUserService.UserId == default ? null : currentUserService.UserId;
        metrics.RecordClientError(request.Source);
        logger.LogWarning("Client error from {ClientErrorSource} at {ClientErrorPath} for user {UserId}: {ClientErrorName}: {ClientErrorMessage}{NewLine}{ClientErrorStack}", request.Source, LogRedactor.Redact(request.Path), userId, LogRedactor.Redact(request.ErrorName), LogRedactor.Redact(request.Message), Environment.NewLine, LogRedactor.Redact(request.Stack));
        return Task.CompletedTask;
    }
}
