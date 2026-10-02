using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Avatar.GetMyAvatarConversation;

public sealed class GetMyAvatarConversationHandler(IAvatarConversationRepository avatarConversationRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, ISessionRepository sessionRepository, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMyAvatarConversationQuery, StudentAvatarConversationDetailResult>
{
    public async Task<StudentAvatarConversationDetailResult> Handle(GetMyAvatarConversationQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        await AvatarGate.EnsureNoExamInProgressAsync(userId, sessionRepository, examsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        var conversation = await avatarConversationRepository.FirstOrDefaultAsync(x => x.Id == request.ConversationId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Messages), asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.AvatarConversationNotFound);
        var subject = conversation.SubjectId is { } subjectId ? await subjectRepository.GetByIdAsync(subjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false) : null;
        var lesson = conversation.LessonId is { } lessonId ? await lessonRepository.GetByIdAsync(lessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false) : null;

        return StudentAvatarConversationResultGenerator.GenerateDetail(conversation, subject?.Name, lesson?.Name);
    }
}
