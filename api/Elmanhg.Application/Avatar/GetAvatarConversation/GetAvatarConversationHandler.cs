using Core.DDD.Repositories;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Avatar.GetAvatarConversation;

public sealed class GetAvatarConversationHandler(IAvatarConversationRepository avatarConversationRepository, IUserRepository userRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository) : IRequestHandler<GetAvatarConversationQuery, AdminAvatarConversationDetailResult>
{
    public async Task<AdminAvatarConversationDetailResult> Handle(GetAvatarConversationQuery request, CancellationToken cancellationToken)
    {
        var conversation = await avatarConversationRepository.GetRequiredAsync(x => x.Id == request.ConversationId, ErrorCodes.AvatarConversationNotFound, cancellationToken, include: query => query.Include(x => x.Messages), asNoTracking: true).ConfigureAwait(false);
        var student = await userRepository.GetByIdAsync(conversation.StudentId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var subject = conversation.SubjectId is { } subjectId ? await subjectRepository.GetByIdAsync(subjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false) : null;
        var unit = conversation.UnitId is { } unitId ? await unitRepository.GetByIdAsync(unitId, cancellationToken, asNoTracking: true).ConfigureAwait(false) : null;
        var lesson = conversation.LessonId is { } lessonId ? await lessonRepository.GetByIdAsync(lessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false) : null;

        return AdminAvatarConversationResultGenerator.GenerateDetail(conversation, student?.DisplayName ?? string.Empty, subject?.Name, unit?.Name, lesson?.Name);
    }
}
