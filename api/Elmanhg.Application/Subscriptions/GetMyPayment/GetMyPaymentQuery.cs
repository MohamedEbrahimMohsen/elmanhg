using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetMyPayment;

public sealed record GetMyPaymentQuery(Guid PaymentId) : IRequest<PaymentResult>;
