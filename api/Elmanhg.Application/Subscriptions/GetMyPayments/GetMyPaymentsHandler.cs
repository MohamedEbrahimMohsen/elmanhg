using Core.DDD.Models;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetMyPayments;

public sealed class GetMyPaymentsHandler(IPaymentRepository paymentRepository, ICurrentUserService currentUserService) : IRequestHandler<GetMyPaymentsQuery, PageData<PaymentResult>>
{
    public async Task<PageData<PaymentResult>> Handle(GetMyPaymentsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var page = await paymentRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId && x.Status != PaymentStatus.Pending, orderBy: query => query.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);

        return page.Map(PaymentResultGenerator.Generate);
    }
}
