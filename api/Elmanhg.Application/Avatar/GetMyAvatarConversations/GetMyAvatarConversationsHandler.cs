using Core.DDD.Models;
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

namespace Elmanhg.Application.Avatar.GetMyAvatarConversations;

public sealed class GetMyAvatarConversationsHandler(IAvatarConversationRepository avatarConversationRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, ISessionRepository sessionRepository, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMyAvatarConversationsQuery, PageData<StudentAvatarConversationResult>>
{
    public async Task<PageData<StudentAvatarConversationResult>> Handle(GetMyAvatarConversationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        await AvatarGate.EnsureNoExamInProgressAsync(userId, sessionRepository, examsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        var page = await avatarConversationRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId, include: query => query.Include(x => x.Messages.Where(m => m.Position == 0)), orderBy: query => query.OrderByDescending(x => x.LastMessageAt).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var subjectIds = page.Items
            .Where(x => x.SubjectId != null)
            .Select(x => x.SubjectId!.Value)
            .Distinct()
            .ToList();
        var lessonIds = page.Items
            .Where(x => x.LessonId != null)
            .Select(x => x.LessonId!.Value)
            .Distinct()
            .ToList();

        var subjects = (await subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var lessons = (await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);

        return new PageData<StudentAvatarConversationResult>
        {
            Items = page.Items
                .Select(x => StudentAvatarConversationResultGenerator.Generate(x, x.SubjectId is { } subjectId ? subjects.GetValueOrDefault(subjectId)?.Name : null, x.LessonId is { } lessonId ? lessons.GetValueOrDefault(lessonId)?.Name : null))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
