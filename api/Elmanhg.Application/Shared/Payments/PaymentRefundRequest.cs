using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Shared.Payments;

public sealed record PaymentRefundRequest(Guid PaymentId, string ProviderTransactionId, Money Amount);
