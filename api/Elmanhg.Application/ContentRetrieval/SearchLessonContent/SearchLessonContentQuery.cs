using Elmanhg.Application.ContentRetrieval.Shared;
using MediatR;

namespace Elmanhg.Application.ContentRetrieval.SearchLessonContent;

public sealed record SearchLessonContentQuery(Guid LessonId, string Query, int? Top, bool IncludeQuestionExplanations = true) : IRequest<LessonContentSearchResult>;
