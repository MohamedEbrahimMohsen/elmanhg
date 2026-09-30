using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Students.GetStudentProfile;

public sealed class GetStudentProfileHandler(IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, ISubjectRepository subjectRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<GetStudentProfileQuery, StudentProfileResult>
{
    public async Task<StudentProfileResult> Handle(GetStudentProfileQuery request, CancellationToken cancellationToken)
    {
        var student = await StudentLookup.GetAsync(userRepository, request.StudentId, cancellationToken).ConfigureAwait(false);
        var subscriptions = await subscriptionRepository.FindAsync(x => x.StudentId == student.Id, cancellationToken, orderBy: query => query.OrderByDescending(x => x.CurrentPeriodStart).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var interestIds = student.SubjectInterestIds;
        List<Subject> interests = interestIds.Count == 0 ? [] : await subjectRepository.FindAsync(x => interestIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var options = subscriptionsOptions.Value;
        var entitlement = StudentEntitlement.Resolve(subscriptions, timeProvider.GetUtcNow(), options.GracePeriod);
        return StudentProfileResultGenerator.Generate(student, interests, subscriptions, entitlement, options.GracePeriod);
    }
}
