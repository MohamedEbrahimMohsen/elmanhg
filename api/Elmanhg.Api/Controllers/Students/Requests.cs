using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Api.Controllers.Students;

public sealed record SubjectInterestsRequest(List<Guid>? SubjectIds);

public sealed record GrantComplimentarySubscriptionRequest(SubscriptionPlan Plan, BillingPeriod Period);
