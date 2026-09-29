using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;

namespace Elmanhg.Application.Students.Shared;

public static class SubjectInterestsResultGenerator
{
    public static SubjectInterestsResult Generate(User user, IReadOnlyList<Subject> subjects)
    {
        return new SubjectInterestsResult(user.NeedsOnboarding, subjects
            .Select(subject => new SubjectInterestResult(subject.Id, subject.Name, user.SubjectInterestIds.Contains(subject.Id)))
            .ToList());
    }
}
