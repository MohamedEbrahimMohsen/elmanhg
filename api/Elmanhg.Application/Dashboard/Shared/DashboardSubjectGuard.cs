using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subjects;

namespace Elmanhg.Application.Dashboard.Shared;

public static class DashboardSubjectGuard
{
    public static async Task EnsureExistsAsync(Guid? subjectId, ISubjectRepository subjectRepository, CancellationToken cancellationToken)
    {
        if (subjectId is not { } id)
        {
            return;
        }

        var subject = await subjectRepository.GetByIdAsync(id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }
    }
}
