using Elmanhg.Application.Teachers.Shared;
using Elmanhg.Domain.Identity;
using MediatR;

namespace Elmanhg.Application.Teachers.GetTeachers;

public sealed class GetTeachersHandler(IUserRepository userRepository) : IRequestHandler<GetTeachersQuery, List<TeacherResult>>
{
    public async Task<List<TeacherResult>> Handle(GetTeachersQuery request, CancellationToken cancellationToken)
    {
        var teachers = await userRepository.FindAsync(x => x.Role == UserRole.Teacher, cancellationToken, orderBy: query => query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        return teachers
            .Select(x => new TeacherResult(x.Id, x.DisplayName))
            .ToList();
    }
}
