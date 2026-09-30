using MediatR;

namespace Elmanhg.Application.EssayGrading.GetDueEssayGradeIds;

public sealed record GetDueEssayGradeIdsQuery : IRequest<List<Guid>>;
