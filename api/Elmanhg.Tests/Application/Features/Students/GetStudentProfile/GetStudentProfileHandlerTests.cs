using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Students.GetStudentProfile;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Students.GetStudentProfile;

public sealed class GetStudentProfileHandlerTests
{
    private static readonly DateTimeOffset Now = SubscriptionBuilder.DefaultStart.AddDays(10);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Subject _physics = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Subject _chemistry = Subject.Create("Chemistry", 2, Guid.NewGuid());
    private readonly Subject _biology = Subject.Create("Biology", 3, Guid.NewGuid());
    private readonly GetStudentProfileHandler _handler;

    public GetStudentProfileHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        List<Subject> subjects = [_biology, _chemistry, _physics];
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => subjects.Where(call.Arg<Expression<Func<Subject, bool>>>().Compile()).ToList());
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new GetStudentProfileHandler(_userRepository, _subscriptionRepository, _subjectRepository, Options.Create(new SubscriptionsOptions { GracePeriodDays = 3 }), _timeProvider);
    }

    [Fact]
    public async Task Handle_Student_ReturnsMaskedProfileEntitlementAndInterests()
    {
        var student = User.CreateStudentWithEmail("Mona", "mona@example.test");
        student.ChooseSubjectInterests([_physics.Id, _biology.Id], Now);
        var paid = SubscriptionRepositoryStub.EntitledBase(student.Id, Now);
        StudentRepositoryStub.Stub(_userRepository, student);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, paid, SubscriptionRepositoryStub.EntitledBase(Guid.NewGuid(), Now));

        var result = await _handler.Handle(new GetStudentProfileQuery(student.Id), TestContext.Current.CancellationToken);

        (result.Id, result.MaskedEmail, result.MaskedPhone, result.Tier, result.HasAskTeacher, result.CanSuspend, result.OnboardedAt).Should().Be((student.Id, "m***@example.test", (string?)null, PlanTier.Base, false, true, (DateTimeOffset?)Now));
        result.SubjectInterests.Should().Equal("Physics", "Biology");
        var subscription = result.Subscriptions.Should().ContainSingle().Subject;
        (subscription.Id, subscription.Plan, subscription.EntitledUntil).Should().Be((paid.Id, SubscriptionPlan.Base, paid.CurrentPeriodEnd.AddDays(3)));
    }

    [Fact]
    public async Task Handle_ComplimentarySubscription_IsFlaggedComplimentary()
    {
        var student = User.CreateStudentWithPhone("Mona", "01012345678");
        var paid = SubscriptionRepositoryStub.EntitledBase(student.Id, Now);
        var complimentary = Subscription.Start(student.Id, SubscriptionPlan.AskTeacher, BillingPeriod.Monthly, 1, Now, paymobReference: null, createdBy: Guid.NewGuid());
        StudentRepositoryStub.Stub(_userRepository, student);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, paid, complimentary);

        var result = await _handler.Handle(new GetStudentProfileQuery(student.Id), TestContext.Current.CancellationToken);

        result.Subscriptions.ToDictionary(x => x.Id, x => x.IsComplimentary).Should().BeEquivalentTo(new Dictionary<Guid, bool> { [paid.Id] = false, [complimentary.Id] = true });
        (result.MaskedPhone, result.HasAskTeacher).Should().Be(("010*****678", true));
        result.SubjectInterests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonStudent_ThrowsStudentNotFound()
    {
        var teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
        StudentRepositoryStub.Stub(_userRepository, teacher);

        var act = () => _handler.Handle(new GetStudentProfileQuery(teacher.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.StudentNotFound);
        await _subscriptionRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>());
    }
}
