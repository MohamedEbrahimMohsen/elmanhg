using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetMyPayment;

public sealed class GetMyPaymentHandler(IPaymentRepository paymentRepository, ICurrentUserService currentUserService) : IRequestHandler<GetMyPaymentQuery, PaymentResult>
{
    public async Task<PaymentResult> Handle(GetMyPaymentQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var payment = await paymentRepository.GetRequiredAsync(x => x.Id == request.PaymentId && x.StudentId == userId, ErrorCodes.PaymentNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return PaymentResultGenerator.Generate(payment);
    }
}
