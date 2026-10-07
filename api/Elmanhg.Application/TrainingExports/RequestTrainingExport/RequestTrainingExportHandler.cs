using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.TrainingExports;
using MediatR;

namespace Elmanhg.Application.TrainingExports.RequestTrainingExport;

public sealed class RequestTrainingExportHandler(ITrainingExportRepository trainingExportRepository, ISubjectRepository subjectRepository, ICurrentUserService currentUserService, TimeProvider timeProvider) : IRequestHandler<RequestTrainingExportCommand, TrainingExportResult>
{
    public async Task<TrainingExportResult> Handle(RequestTrainingExportCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        await DashboardSubjectGuard.EnsureExistsAsync(request.SubjectId, subjectRepository, cancellationToken).ConfigureAwait(false);
        var export = TrainingExport.Request(request.Source, request.From.ToUniversalTime(), request.To.ToUniversalTime(), request.SubjectId, userId, timeProvider.GetUtcNow());

        await trainingExportRepository.AddAsync(export, cancellationToken).ConfigureAwait(false);
        await trainingExportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TrainingExportResultGenerator.Generate(export);
    }
}
