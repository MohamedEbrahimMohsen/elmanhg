using Elmanhg.Domain.Avatar;
using System.Linq.Expressions;

namespace Elmanhg.Application.Avatar.GetAvatarConversations;

public static class GetAvatarConversationsFilter
{
    public static string? Term(GetAvatarConversationsQuery query) => string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLowerInvariant();

    public static Expression<Func<AvatarConversation, bool>> Build(GetAvatarConversationsQuery query, IReadOnlyCollection<Guid> matchingStudentIds)
    {
        var entryPoint = query.EntryPoint;
        var term = Term(query);
        var from = query.From?.ToUniversalTime();
        var to = query.To?.ToUniversalTime();

        return x => (entryPoint == null || x.EntryPoint == entryPoint)
            && (from == null || x.LastMessageAt >= from)
            && (to == null || x.LastMessageAt < to)
            && (term == null || matchingStudentIds.Contains(x.StudentId) || x.Messages.Any(m => m.Text.ToLower().Contains(term)));
    }
}
