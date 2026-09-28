using MediatR;

namespace Elmanhg.Application.Exams.GetExpiredExamSessionIds;

public sealed record GetExpiredExamSessionIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;
