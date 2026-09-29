using Core.DDD.Models;
using Elmanhg.Application.Payments.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Payments.GetPaymentLog;

public sealed record GetPaymentLogQuery(PaymentStatus? Status, SubscriptionPlan? Plan, bool NeedsReview, Guid? StudentId, string? Reference, DateTimeOffset? From, DateTimeOffset? To, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<AdminPaymentResult>>;
