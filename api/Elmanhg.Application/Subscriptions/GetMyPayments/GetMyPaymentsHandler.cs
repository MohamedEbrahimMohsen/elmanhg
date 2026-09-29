using Core.DDD.Models;
using Core.Errors;
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
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var page = await paymentRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId && x.Status != PaymentStatus.Pending, orderBy: query => query.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);

        return new PageData<PaymentResult>
        {
            Items = page.Items
                .Select(PaymentResultGenerator.Generate)
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
