using Core.Errors;

namespace Core.Identity.Tokens.CurrentUser;

public static class CurrentUserServiceExtensions
{
    public static Guid GetRequiredUserId(this ICurrentUserService currentUserService, string errorCode) => currentUserService.UserId is { } userId && userId != Guid.Empty ? userId : throw new UnauthorizedCoreException(errorCode);
}
