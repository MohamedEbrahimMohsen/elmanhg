using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.Shared;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.GetLesson;

public sealed class GetLessonHandler(ILessonRepository lessonRepository) : IRequestHandler<GetLessonQuery, LessonDetailResult>
{
    public async Task<LessonDetailResult> Handle(GetLessonQuery request, CancellationToken cancellationToken)
    {
        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        return LessonResultGenerator.GenerateDetail(lesson);
    }
}
