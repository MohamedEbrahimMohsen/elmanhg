using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.TeacherThreads;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetVoiceDraft;

public sealed class GetVoiceDraftHandler(ITeacherVoiceDraftRepository teacherVoiceDraftRepository, ICurrentUserService currentUserService) : IRequestHandler<GetVoiceDraftQuery, TeacherVoiceDraftResult>
{
    public async Task<TeacherVoiceDraftResult> Handle(GetVoiceDraftQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var draft = await teacherVoiceDraftRepository.FirstOrDefaultAsync(x => x.Id == request.DraftId && x.ThreadId == request.ThreadId && x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherVoiceDraftNotFound);
        return TeacherVoiceDraftResultGenerator.Generate(draft);
    }
}
