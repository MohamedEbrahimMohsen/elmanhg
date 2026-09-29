using MediatR;

namespace Elmanhg.Application.ContentRetrieval.GetStaleLessonContentIds;

public sealed record GetStaleLessonContentIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;
