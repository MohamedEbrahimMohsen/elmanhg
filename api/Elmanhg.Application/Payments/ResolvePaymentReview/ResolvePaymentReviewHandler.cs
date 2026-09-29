using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Payments.ResolvePaymentReview;

public sealed class ResolvePaymentReviewHandler(IPaymentRepository paymentRepository, IUserRepository userRepository, ICurrentUserService currentUserService, TimeProvider timeProvider) : IRequestHandler<ResolvePaymentReviewCommand, AdminPaymentResult>
{
    public async Task<AdminPaymentResult> Handle(ResolvePaymentReviewCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var payment = await paymentRepository.FirstOrDefaultAsync(x => x.Id == request.PaymentId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);
        payment.ResolveReview(currentUserService.UserId.Value, timeProvider.GetUtcNow());
        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var student = await userRepository.FirstOrDefaultAsync(x => x.Id == payment.StudentId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return AdminPaymentResultGenerator.Generate(payment, student);
    }
}
