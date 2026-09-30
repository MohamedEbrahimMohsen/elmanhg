using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;

namespace Elmanhg.Application.Students.Shared;

public static class StudentLookup
{
    public static async Task<User> GetAsync(IUserRepository userRepository, Guid studentId, CancellationToken cancellationToken)
    {
        return await userRepository.FirstOrDefaultAsync(x => x.Id == studentId && x.Role == UserRole.Student, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.StudentNotFound);
    }
}
