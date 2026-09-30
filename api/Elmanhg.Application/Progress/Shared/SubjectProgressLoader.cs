using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Progress.Shared;

public static class SubjectProgressLoader
{
    public static async Task<List<SubjectProgressResult>> LoadAsync(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, Guid studentId, CancellationToken cancellationToken)
    {
        var counts = await questionMasteryRepository.GetLessonCountsAsync(studentId, null, cancellationToken).ConfigureAwait(false);
        var subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false) ?? [];
        var units = await unitRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false) ?? [];
        var bests = await sessionRepository.GetBestExamScoresAsync(studentId, cancellationToken).ConfigureAwait(false);
        return SubjectProgressResultGenerator.Generate(subjects, units, counts, bests);
    }
}
