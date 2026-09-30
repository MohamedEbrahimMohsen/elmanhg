using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Users.Shared;

public sealed record UserSummaryResult(Guid Id, string DisplayName, UserRole Role, UserStatus Status, string? MaskedPhone, string? MaskedEmail, bool InvitationPending, bool CanSuspend, DateTimeOffset CreationDate, PlanTier? Tier, bool HasAskTeacher, List<Guid> SubjectIds);
