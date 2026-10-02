using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.GetStudentUnit;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentUnit;

public sealed class GetStudentUnitHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _unit;
    private readonly List<Lesson> _lessons = [];
    private readonly GetStudentUnitHandler _handler;

    public GetStudentUnitHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _unit = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        _unitRepository.GetByIdAsync(_unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_unit);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([]);
        _sessionRepository.GetBestExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns([]);
        _timeProvider.GetUtcNow().Returns(Now);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));
        _handler = new GetStudentUnitHandler(_unitRepository, _subjectRepository, _lessonRepository, _questionMasteryRepository, _sessionRepository, _subscriptionRepository, Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService, new FakeRuntimeSettings());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetStudentUnitQuery(_unit.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownUnit_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new GetStudentUnitQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
    }

    [Fact]
    public async Task Handle_UnitOfMissingSubject_ThrowsUnitNotFound()
    {
        var orphan = CurriculumUnit.Create(Subject.Create("Gone", 2, Guid.NewGuid()), "Orphan", 1, Guid.NewGuid());
        _unitRepository.GetByIdAsync(orphan.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(orphan);

        var act = () => _handler.Handle(new GetStudentUnitQuery(orphan.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
    }

    [Fact]
    public async Task Handle_Unit_ReturnsPublishedLessonsWithMastery()
    {
        var forces = AddLesson("Forces", LessonState.Published);
        AddLesson("Draft", LessonState.Draft);
        var energy = AddLesson("Energy", LessonState.Published);
        AddLesson("Old", LessonState.Archived);
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([Count(forces, 4, 2, 3), Count(Guid.NewGuid(), 6, 6, 6)]);
        _sessionRepository.GetBestExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns([new ExamBestScore(new UnitExamScope(_unit.Id).ToKey(), 72.4m)]);

        var result = await _handler.Handle(new GetStudentUnitQuery(_unit.Id), TestContext.Current.CancellationToken);

        result.Lessons.Should().Equal(new StudentLessonSummaryResult(forces.Id, "Forces", 4, 2, 3, 50, false), new StudentLessonSummaryResult(energy.Id, "Energy", 0, 0, 0, 0, false));
        (result.Id, result.Name, result.SubjectId, result.SubjectName).Should().Be((_unit.Id, "Mechanics", _subject.Id, "Physics"));
        (result.ServableCount, result.MasteredCount, result.SeenCount, result.MasteryPercent, result.BestExamScorePercent).Should().Be((10, 8, 9, 80, 72.4m));
    }

    [Fact]
    public async Task Handle_NoUnitExam_ReturnsNullBestScore()
    {
        _sessionRepository.GetBestExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns([new ExamBestScore(new UnitExamScope(Guid.NewGuid()).ToKey(), 55m)]);

        var result = await _handler.Handle(new GetStudentUnitQuery(_unit.Id), TestContext.Current.CancellationToken);

        result.BestExamScorePercent.Should().BeNull();
    }

    private Lesson AddLesson(string name, LessonState state)
    {
        var lesson = Lesson.Create(_unit, name, _lessons.Count + 1, Guid.NewGuid());
        if (state != LessonState.Draft)
        {
            lesson.Publish(Guid.NewGuid());
        }

        if (state == LessonState.Archived)
        {
            lesson.Archive(Guid.NewGuid());
        }

        _lessons.Add(lesson);
        return lesson;
    }

    private LessonMasteryCount Count(Lesson lesson, int servable, int mastered, int seen) => Count(lesson.Id, servable, mastered, seen);

    private LessonMasteryCount Count(Guid lessonId, int servable, int mastered, int seen) => new(_subject.Id, 1, _unit.Id, 1, lessonId, 1, servable, mastered, seen);
}
