using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.GetStudentLesson;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentLesson;

public sealed class GetStudentLessonHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _mechanics;
    private readonly CurriculumUnit _waves;
    private readonly List<CurriculumUnit> _units;
    private readonly List<Lesson> _lessons = [];
    private readonly GetStudentLessonHandler _handler;

    public GetStudentLessonHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _mechanics = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        _waves = CurriculumUnit.Create(_subject, "Waves", 2, Guid.NewGuid());
        _units = [_mechanics, _waves];
        _lessonRepository.GetWithObjectivesAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => _lessons.FirstOrDefault(x => x.Id == call.Arg<Guid>()));
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _unitRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(call => _units.FirstOrDefault(x => x.Id == call.Arg<Guid>()));
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => _units.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([]);
        _timeProvider.GetUtcNow().Returns(Now);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));
        _handler = new GetStudentLessonHandler(_lessonRepository, _unitRepository, _subjectRepository, _questionMasteryRepository, _subscriptionRepository, Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var lesson = AddLesson(_mechanics, "Energy", 1, LessonState.Published);
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new GetStudentLessonQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_DraftLesson_ThrowsLessonNotFound()
    {
        var lesson = AddLesson(_mechanics, "Draft", 1, LessonState.Draft);

        var act = () => _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_ArchivedLesson_ThrowsLessonNotFound()
    {
        var lesson = AddLesson(_mechanics, "Old", 1, LessonState.Archived);

        var act = () => _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_LessonOfMissingUnit_ThrowsLessonNotFound()
    {
        var orphanUnit = CurriculumUnit.Create(_subject, "Gone", 3, Guid.NewGuid());
        var lesson = AddLesson(orphanUnit, "Orphan", 1, LessonState.Published);

        var act = () => _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_LessonOfMissingSubject_ThrowsLessonNotFound()
    {
        var orphanUnit = CurriculumUnit.Create(Subject.Create("Gone", 2, Guid.NewGuid()), "Orphan", 1, Guid.NewGuid());
        _units.Add(orphanUnit);
        var lesson = AddLesson(orphanUnit, "Orphan", 1, LessonState.Published);

        var act = () => _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_PublishedLesson_ReturnsContentObjectivesAndBreadcrumb()
    {
        var lesson = Lesson.Create(_mechanics, "Energy", 1, Guid.NewGuid());
        lesson.Update("Energy", "<p>Energy is conserved.</p>", "<p>Summary.</p>", "https://www.youtube.com/watch?v=x", [new LessonObjectiveContent(null, "Define energy"), new LessonObjectiveContent(null, "Apply conservation")], Guid.NewGuid());
        lesson.Publish(Guid.NewGuid());
        _lessons.Add(lesson);

        var result = await _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (result.Id, result.Name, result.Explanation, result.Summary, result.VideoUrl).Should().Be((lesson.Id, "Energy", "<p>Energy is conserved.</p>", "<p>Summary.</p>", "https://www.youtube.com/watch?v=x"));
        result.Objectives.Select(x => (x.Text, x.Order)).Should().Equal(("Define energy", 1), ("Apply conservation", 2));
        (result.UnitId, result.UnitName, result.SubjectId, result.SubjectName).Should().Be((_mechanics.Id, "Mechanics", _subject.Id, "Physics"));
    }

    [Fact]
    public async Task Handle_PublishedLesson_ReturnsLessonMastery()
    {
        var lesson = AddLesson(_mechanics, "Energy", 1, LessonState.Published);
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([new LessonMasteryCount(_subject.Id, 1, _mechanics.Id, 1, lesson.Id, 1, 4, 1, 3), new LessonMasteryCount(_subject.Id, 1, _mechanics.Id, 1, Guid.NewGuid(), 2, 9, 9, 9)]);

        var result = await _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        (result.ServableCount, result.MasteredCount, result.SeenCount, result.MasteryPercent).Should().Be((4, 1, 3, 25));
    }

    [Fact]
    public async Task Handle_LastLessonOfUnit_NextIsFirstLessonOfNextUnit()
    {
        var forces = AddLesson(_mechanics, "Forces", 1, LessonState.Published);
        var energy = AddLesson(_mechanics, "Energy", 2, LessonState.Published);
        AddLesson(_waves, "Draft wave", 1, LessonState.Draft);
        var waves = AddLesson(_waves, "Wave basics", 2, LessonState.Published);

        var result = await _handler.Handle(new GetStudentLessonQuery(energy.Id), TestContext.Current.CancellationToken);

        result.PreviousLesson.Should().Be(new LessonLinkResult(forces.Id, "Forces", _mechanics.Id, "Mechanics"));
        result.NextLesson.Should().Be(new LessonLinkResult(waves.Id, "Wave basics", _waves.Id, "Waves"));
    }

    [Fact]
    public async Task Handle_OnlyPublishedLesson_HasNoNeighbours()
    {
        var lesson = AddLesson(_mechanics, "Energy", 1, LessonState.Published);
        AddLesson(_waves, "Draft wave", 1, LessonState.Draft);

        var result = await _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        result.PreviousLesson.Should().BeNull();
        result.NextLesson.Should().BeNull();
    }

    private Lesson AddLesson(CurriculumUnit unit, string name, int order, LessonState state)
    {
        var lesson = Lesson.Create(unit, name, order, Guid.NewGuid());
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
}
