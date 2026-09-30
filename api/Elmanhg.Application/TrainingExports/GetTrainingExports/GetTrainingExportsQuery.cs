using Core.DDD.Models;
using Elmanhg.Application.TrainingExports.Shared;
using MediatR;

namespace Elmanhg.Application.TrainingExports.GetTrainingExports;

public sealed record GetTrainingExportsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TrainingExportResult>>;
