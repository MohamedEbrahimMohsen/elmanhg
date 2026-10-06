using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var payment = await paymentRepository.GetRequiredAsync(x => x.Id == request.PaymentId, ErrorCodes.PaymentNotFound, cancellationToken).ConfigureAwait(false);
        payment.ResolveReview(userId, timeProvider.GetUtcNow());
        await paymentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var student = await userRepository.FirstOrDefaultAsync(x => x.Id == payment.StudentId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return AdminPaymentResultGenerator.Generate(payment, student);
    }
}
