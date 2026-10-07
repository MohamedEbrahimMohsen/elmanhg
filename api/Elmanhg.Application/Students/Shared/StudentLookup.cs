using Core.DDD.Repositories;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;

namespace Elmanhg.Application.Students.Shared;

public static class StudentLookup
{
    public static async Task<User> GetAsync(IUserRepository userRepository, Guid studentId, CancellationToken cancellationToken)
    {
        return await userRepository.GetRequiredAsync(x => x.Id == studentId && x.Role == UserRole.Student, ErrorCodes.StudentNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
    }
}
