using Core.DDD.Repositories;
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
        var unit = await unitRepository.GetRequiredAsync(request.UnitId, ErrorCodes.UnitNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var lessons = await lessonRepository.FindAsync(x => x.UnitId == unit.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var lessonIds = lessons
            .Select(x => x.Id)
            .ToList();
        var questionCounts = await questionRepository.CountByLessonAsync(lessonIds, cancellationToken).ConfigureAwait(false);
        var servableCounts = await questionRepository.CountServableByLessonAsync(lessonIds, cancellationToken).ConfigureAwait(false);

        return lessons
            .Select(x => LessonResultGenerator.Generate(x, questionCounts.GetValueOrDefault(x.Id), servableCounts.GetValueOrDefault(x.Id)))
            .ToList();
    }
}
