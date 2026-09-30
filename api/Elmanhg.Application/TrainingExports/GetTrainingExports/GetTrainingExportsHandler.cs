using Core.DDD.Models;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.TrainingExports;
using MediatR;

namespace Elmanhg.Application.TrainingExports.GetTrainingExports;

public sealed class GetTrainingExportsHandler(ITrainingExportRepository trainingExportRepository) : IRequestHandler<GetTrainingExportsQuery, PageData<TrainingExportResult>>
{
    public async Task<PageData<TrainingExportResult>> Handle(GetTrainingExportsQuery request, CancellationToken cancellationToken)
    {
        var page = await trainingExportRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, orderBy: query => query.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);

        return new PageData<TrainingExportResult>
        {
            Items = page.Items
                .Select(TrainingExportResultGenerator.Generate)
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
