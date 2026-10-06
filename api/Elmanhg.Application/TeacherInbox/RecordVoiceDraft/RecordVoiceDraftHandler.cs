using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherInbox.RecordVoiceDraft;

public sealed class RecordVoiceDraftHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherVoiceDraftRepository teacherVoiceDraftRepository, ITeacherSubjectRepository teacherSubjectRepository, IFileStorage fileStorage, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<RecordVoiceDraftCommand, TeacherVoiceDraftResult>
{
    public async Task<TeacherVoiceDraftResult> Handle(RecordVoiceDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var thread = await teacherThreadRepository.GetRequiredAsync(x => x.Id == request.ThreadId, ErrorCodes.TeacherThreadNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        await TeacherInboxAccess.EnsureCanAccessAsync(thread.SubjectId, userId, currentUserService.GetClaim(ClaimTypes.Role), teacherSubjectRepository, cancellationToken).ConfigureAwait(false);
        thread.EnsureCanReply(userId);

        var audio = request.Audio!;
        var key = $"{TeacherThreadImageFormats.StorageFolder}/{Guid.NewGuid():N}{Path.GetExtension(audio.FileName).ToLowerInvariant()}";
        await using var content = audio.OpenReadStream();
        var url = await fileStorage.SaveAsync(content, key, cancellationToken).ConfigureAwait(false);

        var draft = TeacherVoiceDraft.Record(thread.Id, userId, key, url, request.DurationSeconds, timeProvider.GetUtcNow());
        await teacherVoiceDraftRepository.AddAsync(draft, cancellationToken).ConfigureAwait(false);
        await teacherVoiceDraftRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeacherVoiceDraftResultGenerator.Generate(draft);
    }
}
