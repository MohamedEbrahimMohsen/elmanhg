using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Lessons.GetLessons;

public sealed class GetLessonsHandler(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository) : IRequestHandler<GetLessonsQuery, List<LessonResult>>
{
    public async Task<List<LessonResult>> Handle(GetLessonsQuery request, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var lessons = await lessonRepository.FindAsync(x => x.UnitId == unit.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var questionCounts = await questionRepository.CountByLessonAsync(lessons.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);

        return lessons
            .Select(x => LessonResultGenerator.Generate(x, questionCounts.GetValueOrDefault(x.Id)))
            .ToList();
    }
}
