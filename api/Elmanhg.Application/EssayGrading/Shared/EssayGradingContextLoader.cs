using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.EssayGrading.Shared;

public static class EssayGradingContextLoader
{
    public static async Task<EssayGradingContext?> LoadAsync(Guid lessonId, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, CancellationToken cancellationToken)
    {
        var lesson = await lessonRepository.FirstOrDefaultAsync(x => x.Id == lessonId, cancellationToken, include: q => q.Include(x => x.Objectives), asNoTracking: true).ConfigureAwait(false);
        if (lesson is null)
        {
            return null;
        }

        var unit = await unitRepository.FirstOrDefaultAsync(x => x.Id == lesson.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            return null;
        }

        var subject = await subjectRepository.FirstOrDefaultAsync(x => x.Id == unit.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            return null;
        }

        var objectives = lesson.Objectives
            .OrderBy(x => x.Order)
            .Select(x => x.Text)
            .ToList();
        return new EssayGradingContext(subject.Name, objectives);
    }
}
