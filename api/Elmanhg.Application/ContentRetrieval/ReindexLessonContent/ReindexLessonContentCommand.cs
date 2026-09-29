using MediatR;

namespace Elmanhg.Application.ContentRetrieval.ReindexLessonContent;

public sealed record ReindexLessonContentCommand(Guid LessonId) : IRequest;
