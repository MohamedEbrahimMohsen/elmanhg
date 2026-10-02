using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.CreateTeacherThread;

public sealed class CreateTeacherThreadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _content = new();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly CreateTeacherThreadHandler _handler;

    public CreateTeacherThreadHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _content.Lesson.Publish(Guid.NewGuid());
        _lessonRepository.GetByIdAsync(_content.Lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_content.Lesson);
        _unitRepository.GetByIdAsync(_content.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_content.Unit);
        _subjectRepository.GetByIdAsync(_content.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_content.Subject);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now), new SubscriptionBuilder().ForStudent(_studentId).WithPlan(SubscriptionPlan.AskTeacher).StartingAt(Now.AddDays(-1)).Build());
        _handler = new CreateTeacherThreadHandler(_teacherThreadRepository, _subscriptionRepository, _lessonRepository, _unitRepository, _subjectRepository, Substitute.For<IQuestionRepository>(), Substitute.For<ISessionRepository>(), _fileStorage, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService, new FakeRuntimeSettings());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Command());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutAskTeacher_ThrowsAskTeacherRequiresSubscription()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));

        var act = () => Handle(Command(image: Image("photo.png")));

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherRequiresSubscription);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MonthlyLimitReached_ThrowsAskTeacherMonthlyLimitReached()
    {
        _teacherThreadRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TeacherThread, bool>>>()).Returns(20);

        var act = () => Handle(Command());

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherMonthlyLimitReached);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonContext_AddsOpenThreadAndReturnsResult()
    {
        var result = await Handle(Command());

        await _teacherThreadRepository.Received(1).AddAsync(Arg.Is<TeacherThread>(x => x.StudentId == _studentId && x.SubjectId == _content.Subject.Id && x.SlaDueAt == Now.AddHours(24)), Arg.Any<CancellationToken>());
        (result.Status, result.IsOverdue, result.Context.LessonId).Should().Be((TeacherThreadStatus.Open, false, _content.Lesson.Id));
        result.Messages.Should().ContainSingle().Which.IsFromStudent.Should().BeTrue();
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithImage_StoresUnderTeacherThreadsKeyAndAttachesUrl()
    {
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("/api/media/teacher-threads/stored.png");

        var result = await Handle(Command(image: Image("Photo.PNG")));

        await _fileStorage.Received(1).SaveAsync(Arg.Any<Stream>(), Arg.Is<string>(k => k.StartsWith("teacher-threads/") && k.EndsWith(".png")), Arg.Any<CancellationToken>());
        result.Messages.Should().ContainSingle().Which.ImageUrl.Should().Be("/api/media/teacher-threads/stored.png");
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFoundAndStoresNothing()
    {
        var act = () => Handle(Command(lessonId: Guid.NewGuid(), image: Image("photo.png")));

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private CreateTeacherThreadCommand Command(Guid? lessonId = null, IFormFile? image = null) => new("Why is F = ma?", lessonId ?? _content.Lesson.Id, null, null, image);

    private Task<TeacherThreadResult> Handle(CreateTeacherThreadCommand command) => _handler.Handle(command, TestContext.Current.CancellationToken);

    private static FormFile Image(string fileName) => new(new MemoryStream([0x89, 0x50, 0x4E, 0x47]), 0, 4, "image", fileName) { Headers = new HeaderDictionary(), ContentType = "image/png" };
}
