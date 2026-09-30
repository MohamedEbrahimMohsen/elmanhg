using Elmanhg.Domain.Identity;

namespace Elmanhg.Application.Shared.Email;

public sealed record InvitationEmail(string Email, string DisplayName, UserRole Role);
