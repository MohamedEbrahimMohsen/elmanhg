using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Students.Shared;

public sealed record StudentProfileResult(Guid Id, string DisplayName, string? MaskedPhone, string? MaskedEmail, UserStatus Status, bool CanSuspend, DateTimeOffset CreationDate, DateTimeOffset? OnboardedAt, List<string> SubjectInterests, PlanTier Tier, bool HasAskTeacher, List<AdminSubscriptionResult> Subscriptions);
