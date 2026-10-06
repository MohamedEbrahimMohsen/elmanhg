using Core.DDD.Models;
using Elmanhg.Application.Payments.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Payments.GetPaymentLog;

public sealed class GetPaymentLogHandler(IPaymentRepository paymentRepository, IUserRepository userRepository) : IRequestHandler<GetPaymentLogQuery, PageData<AdminPaymentResult>>
{
    public async Task<PageData<AdminPaymentResult>> Handle(GetPaymentLogQuery request, CancellationToken cancellationToken)
    {
        var page = await paymentRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetPaymentLogFilter.Build(request), orderBy: query => query.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var studentIds = page.Items
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var students = await userRepository.FindAsync(x => studentIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var studentsById = students.ToDictionary(x => x.Id);

        return page.Map(x => AdminPaymentResultGenerator.Generate(x, studentsById.GetValueOrDefault(x.StudentId)));
    }
}
