using Elmanhg.Application.SlaCalendars.Shared;
using Elmanhg.Domain.SlaCalendars;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.GetExamPeriods;

public sealed class GetExamPeriodsHandler(IExamPeriodRepository examPeriodRepository) : IRequestHandler<GetExamPeriodsQuery, List<ExamPeriodResult>>
{
    public async Task<List<ExamPeriodResult>> Handle(GetExamPeriodsQuery request, CancellationToken cancellationToken)
    {
        var periods = await examPeriodRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderByDescending(x => x.StartDate).ThenBy(x => x.Id), asNoTracking: true).ConfigureAwait(false) ?? [];
        return periods
            .Select(ExamPeriodResultGenerator.Generate)
            .ToList();
    }
}
