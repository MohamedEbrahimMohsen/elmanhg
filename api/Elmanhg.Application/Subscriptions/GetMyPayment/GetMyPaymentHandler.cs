using Core.Errors;
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
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var payment = await paymentRepository.FirstOrDefaultAsync(x => x.Id == request.PaymentId && x.StudentId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);
        return PaymentResultGenerator.Generate(payment);
    }
}
