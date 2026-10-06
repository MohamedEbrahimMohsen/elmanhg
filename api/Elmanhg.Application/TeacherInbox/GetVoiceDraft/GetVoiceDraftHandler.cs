using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var draft = await teacherVoiceDraftRepository.GetRequiredAsync(x => x.Id == request.DraftId && x.ThreadId == request.ThreadId && x.TeacherId == userId, ErrorCodes.TeacherVoiceDraftNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return TeacherVoiceDraftResultGenerator.Generate(draft);
    }
}
