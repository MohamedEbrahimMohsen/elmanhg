using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherInbox.SendVoiceReply;

public sealed class SendVoiceReplyHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherVoiceDraftRepository teacherVoiceDraftRepository, ITeacherSubjectRepository teacherSubjectRepository, IUserRepository userRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<SendVoiceReplyCommand, TeacherInboxThreadResult>
{
    public async Task<TeacherInboxThreadResult> Handle(SendVoiceReplyCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherThreadNotFound);
        await TeacherInboxAccess.EnsureCanAccessAsync(thread.SubjectId, userId, currentUserService.GetClaim(ClaimTypes.Role), teacherSubjectRepository, cancellationToken).ConfigureAwait(false);
        var draft = await teacherVoiceDraftRepository.FirstOrDefaultAsync(x => x.Id == request.DraftId && x.ThreadId == thread.Id && x.TeacherId == userId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherVoiceDraftNotFound);

        var now = timeProvider.GetUtcNow();
        var message = thread.ReplyWithVoice(userId, request.Text ?? string.Empty, draft.AudioUrl, draft.AudioDurationSeconds, now);
        draft.MarkSent(message.Id, now);

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var names = await TeacherInboxNames.LoadAsync(userRepository, [thread], cancellationToken).ConfigureAwait(false);
        return TeacherInboxResultGenerator.GenerateThread(thread, names, userId, now);
    }
}
