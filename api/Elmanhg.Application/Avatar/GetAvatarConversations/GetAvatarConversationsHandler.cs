using Core.DDD.Models;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Avatar.GetAvatarConversations;

public sealed class GetAvatarConversationsHandler(IAvatarConversationRepository avatarConversationRepository, IUserRepository userRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository) : IRequestHandler<GetAvatarConversationsQuery, PageData<AdminAvatarConversationResult>>
{
    public async Task<PageData<AdminAvatarConversationResult>> Handle(GetAvatarConversationsQuery request, CancellationToken cancellationToken)
    {
        var term = GetAvatarConversationsFilter.Term(request);
        List<Guid> matching = term is null ? [] : (await userRepository.FindAsync(x => x.DisplayName.ToLower().Contains(term), cancellationToken, asNoTracking: true).ConfigureAwait(false))
            .Select(x => x.Id)
            .ToList();

        var page = await avatarConversationRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetAvatarConversationsFilter.Build(request, matching), include: query => query.Include(x => x.Messages.Where(m => m.Position == 0)), orderBy: query => query.OrderByDescending(x => x.LastMessageAt).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var studentIds = page.Items
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();
        var lessonIds = page.Items
            .Where(x => x.LessonId != null)
            .Select(x => x.LessonId!.Value)
            .Distinct()
            .ToList();
        var subjectIds = page.Items
            .Where(x => x.SubjectId != null)
            .Select(x => x.SubjectId!.Value)
            .Distinct()
            .ToList();

        var students = (await userRepository.FindAsync(x => studentIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var lessons = (await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var subjects = (await subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false)).ToDictionary(x => x.Id);

        return new PageData<AdminAvatarConversationResult>
        {
            Items = page.Items
                .Select(x => AdminAvatarConversationResultGenerator.Generate(x, students.GetValueOrDefault(x.StudentId)?.DisplayName ?? string.Empty, x.SubjectId is { } subjectId ? subjects.GetValueOrDefault(subjectId)?.Name : null, x.LessonId is { } lessonId ? lessons.GetValueOrDefault(lessonId)?.Name : null))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
