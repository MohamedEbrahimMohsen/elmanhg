namespace Elmanhg.Application.Auth.Shared;

public sealed record AuthUserResult(Guid Id, string DisplayName, string Role, string? PhoneNumber, string? Email, bool NeedsOnboarding);
