using Elmanhg.Application.QuestionValidation.Shared;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.GetValidationQueueFilters;

public sealed record GetValidationQueueFiltersQuery : IRequest<ValidationQueueFiltersResult>;
