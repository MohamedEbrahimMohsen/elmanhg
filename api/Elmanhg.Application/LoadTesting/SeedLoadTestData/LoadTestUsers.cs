using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public static class LoadTestUsers
{
    public static async Task<User> EnsureTeacherAsync(UserManager<User> userManager, string key)
    {
        var email = LoadTestData.TeacherEmail(key);
        var existing = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var teacher = User.CreateTeacher("Load test teacher", email);
        EnsureSucceeded(await userManager.CreateAsync(teacher).ConfigureAwait(false));
        return teacher;
    }

    public static async Task<User?> CreateStudentAsync(UserManager<User> userManager, string key, int index, string password, Guid subjectId, DateTimeOffset now)
    {
        var email = LoadTestData.StudentEmail(key, index);
        if (await userManager.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            return null;
        }

        var student = User.CreateStudentWithEmail(LoadTestData.StudentDisplayName(index), email);
        student.ChooseSubjectInterests([subjectId], now);
        EnsureSucceeded(await userManager.CreateAsync(student, password).ConfigureAwait(false));
        return student;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InternalServerErrorCoreException(ErrorCodes.LoadTestSeedFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))));
        }
    }
}
