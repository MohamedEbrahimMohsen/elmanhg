using Elmanhg.Domain.Identity;
using System.Linq.Expressions;

namespace Elmanhg.Application.Users.GetUsers;

public static class GetUsersFilter
{
    public static Expression<Func<User, bool>> Build(GetUsersQuery query)
    {
        var role = query.Role;
        var status = query.Status;
        var term = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var lowered = term?.ToLowerInvariant();
        var normalizedEmail = term?.ToUpperInvariant();

        return x => x.Role == role
            && (status == null || x.Status == status)
            && (term == null || x.DisplayName.ToLower().Contains(lowered!) || x.PhoneNumber == term || x.NormalizedEmail == normalizedEmail);
    }
}
