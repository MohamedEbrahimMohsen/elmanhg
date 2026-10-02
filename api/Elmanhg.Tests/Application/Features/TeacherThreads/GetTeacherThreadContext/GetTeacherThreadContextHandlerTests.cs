using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetTeacherThreadContext;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetTeacherThreadContext;

public sealed class GetTeacherThreadContextHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _content = new();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly GetTeacherThreadContextHandler _handler;

    public GetTeacherThreadContextHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _content.Lesson.Publish(Guid.NewGuid());
        _lessonRepository.GetByIdAsync(_content.Lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_content.Lesson);
        _unitRepository.GetByIdAsync(_content.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_content.Unit);
        _subjectRepository.GetByIdAsync(_content.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_content.Subject);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now), new SubscriptionBuilder().ForStudent(_studentId).WithPlan(SubscriptionPlan.AskTeacher).StartingAt(Now.AddDays(-1)).Build());
        _handler = new GetTeacherThreadContextHandler(_lessonRepository, _unitRepository, _subjectRepository, Substitute.For<IQuestionRepository>(), Substitute.For<ISessionRepository>(), _subscriptionRepository, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService, new FakeRuntimeSettings());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetTeacherThreadContextQuery(_content.Lesson.Id, null, null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_BaseWithoutAddOn_ThrowsAskTeacherRequiresSubscription()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));

        var act = () => _handler.Handle(new GetTeacherThreadContextQuery(_content.Lesson.Id, null, null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherRequiresSubscription);
        await _lessonRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_PublishedLesson_ReturnsContextResult()
    {
        var result = await _handler.Handle(new GetTeacherThreadContextQuery(_content.Lesson.Id, null, null), TestContext.Current.CancellationToken);

        (result.SubjectId, result.SubjectName, result.UnitId, result.UnitName, result.LessonId, result.LessonName, result.QuestionId).Should().Be((_content.Subject.Id, "Physics", _content.Unit.Id, "Mechanics", _content.Lesson.Id, "Newton's laws", (Guid?)null));
    }
}
