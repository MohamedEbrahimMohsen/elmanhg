using MediatR;

namespace Elmanhg.Application.Browse.RecordLessonOpening;

public sealed record RecordLessonOpeningCommand(Guid LessonId) : IRequest;
