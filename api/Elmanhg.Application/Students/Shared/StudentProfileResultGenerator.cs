using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Students.Shared;

public static class StudentProfileResultGenerator
{
    public static StudentProfileResult Generate(User student, IReadOnlyList<Subject> interests, IReadOnlyList<Subscription> subscriptions, StudentEntitlement entitlement, TimeSpan gracePeriod)
    {
        var interestNames = interests
            .OrderBy(x => x.Order)
            .Select(x => x.Name)
            .ToList();
        var subscriptionResults = subscriptions
            .Select(x => AdminSubscriptionResultGenerator.Generate(x, gracePeriod))
            .ToList();
        return new StudentProfileResult(student.Id, student.DisplayName, ContactMask.MaskPhone(student.PhoneNumber), ContactMask.MaskEmail(student.Email), student.Status, student.IsActive, student.CreationDate, student.OnboardedAt, interestNames, entitlement.Tier, entitlement.HasAskTeacher, subscriptionResults);
    }
}
