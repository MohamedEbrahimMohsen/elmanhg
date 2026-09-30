using Core.DDD.Models;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Progress.GetStudentSessionHistory;

public sealed class GetStudentSessionHistoryHandler(IUserRepository userRepository, ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository) : IRequestHandler<GetStudentSessionHistoryQuery, PageData<SessionHistoryItemResult>>
{
    public async Task<PageData<SessionHistoryItemResult>> Handle(GetStudentSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        var student = await StudentLookup.GetAsync(userRepository, request.StudentId, cancellationToken).ConfigureAwait(false);
        return await SessionHistoryLoader.LoadAsync(sessionRepository, lessonRepository, unitRepository, student.Id, request.Kind, request.PageNumber, request.PageSize, cancellationToken).ConfigureAwait(false);
    }
}
