using MediatR;

namespace Elmanhg.Application.MathStepGrading.GetDueMathStepGradeIds;

public sealed record GetDueMathStepGradeIdsQuery : IRequest<List<Guid>>;
