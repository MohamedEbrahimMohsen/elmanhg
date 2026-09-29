using Core.DDD.Models;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetMyPayments;

public sealed record GetMyPaymentsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<PaymentResult>>;
