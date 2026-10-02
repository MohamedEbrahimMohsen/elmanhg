using MediatR;

namespace Elmanhg.Application.Payments.GetPaymentSettings;

public sealed record GetPaymentSettingsQuery : IRequest<PaymentSettingsResult>;
