using Elmanhg.Application.Progress.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Progress.GetStudentProgress;

public sealed class GetStudentProgressHandler(IUserRepository userRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IOptions<ProgressOptions> progressOptions) : IRequestHandler<GetStudentProgressQuery, StudentProgressResult>
{
    public async Task<StudentProgressResult> Handle(GetStudentProgressQuery request, CancellationToken cancellationToken)
    {
        var student = await StudentLookup.GetAsync(userRepository, request.StudentId, cancellationToken).ConfigureAwait(false);
        var subjects = await SubjectProgressLoader.LoadAsync(questionMasteryRepository, sessionRepository, subjectRepository, unitRepository, student.Id, cancellationToken).ConfigureAwait(false);
        var weakSpots = await WeakSpotsLoader.LoadAsync(questionMasteryRepository, lessonRepository, subjectRepository, progressOptions.Value, student.Id, cancellationToken).ConfigureAwait(false);
        return new StudentProgressResult(subjects, weakSpots);
    }
}
