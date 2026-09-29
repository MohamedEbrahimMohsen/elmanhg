using MediatR;

namespace Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;

public sealed record GetDueSlaThreadIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;
