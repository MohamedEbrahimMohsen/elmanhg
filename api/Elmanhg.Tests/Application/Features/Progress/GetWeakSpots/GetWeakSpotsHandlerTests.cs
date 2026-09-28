using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetWeakSpots;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Progress.GetWeakSpots;

public sealed class GetWeakSpotsHandlerTests
{
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ProgressOptions _options = new();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly QuestionBuilder _builder = new();
    private readonly List<Lesson> _lessons = [];
    private readonly GetWeakSpotsHandler _handler;

    public GetWeakSpotsHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _lessons.Add(_builder.Lesson);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new List<Subject> { _builder.Subject }.Where(call.Arg<Expression<Func<Subject, bool>>>().Compile()).ToList());
        StubLessonCounts([]);
        StubObjectiveCounts([]);
        _handler = new GetWeakSpotsHandler(_questionMasteryRepository, _lessonRepository, _subjectRepository, Microsoft.Extensions.Options.Options.Create(_options), _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetWeakSpotsQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_NothingAttempted_ReturnsEmptyListsWithoutLoadingLessons()
    {
        StubLessonCounts([LessonCount(_builder.Lesson, 4, 0, 0)]);
        StubObjectiveCounts([ObjectiveCount(_builder.ObjectiveId, 1, 4, 0, 0)]);

        var result = await _handler.Handle(new GetWeakSpotsQuery(), TestContext.Current.CancellationToken);

        result.Lessons.Should().BeEmpty();
        result.Objectives.Should().BeEmpty();
        await _lessonRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_WeakLessons_ReturnsNamesSubjectAndPercentInPickOrder()
    {
        var waves = AddLesson("Waves", 2);
        StubLessonCounts([LessonCount(_builder.Lesson, 3, 2, 3), LessonCount(waves, 7, 3, 5)]);

        var result = await _handler.Handle(new GetWeakSpotsQuery(), TestContext.Current.CancellationToken);

        result.Lessons.Should().Equal(new WeakLessonResult(waves.Id, "Waves", _builder.Subject.Id, "Physics", 7, 3, 5, 42), new WeakLessonResult(_builder.Lesson.Id, "Newton's laws", _builder.Subject.Id, "Physics", 3, 2, 3, 66));
    }

    [Fact]
    public async Task Handle_WeakObjective_ReturnsObjectiveTextWithLesson()
    {
        StubObjectiveCounts([ObjectiveCount(_builder.ObjectiveId, 1, 3, 1, 2)]);

        var result = await _handler.Handle(new GetWeakSpotsQuery(), TestContext.Current.CancellationToken);

        result.Objectives.Should().Equal(new WeakObjectiveResult(_builder.ObjectiveId, "State the first law", _builder.Lesson.Id, "Newton's laws", _builder.Subject.Id, "Physics", 3, 1, 2, 33));
        result.Lessons.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LessonNoLongerLoaded_SkipsEntry()
    {
        _lessons.Clear();
        StubLessonCounts([LessonCount(_builder.Lesson, 2, 0, 1)]);

        var result = await _handler.Handle(new GetWeakSpotsQuery(), TestContext.Current.CancellationToken);

        result.Lessons.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ConfiguredCounts_LimitsBothLists()
    {
        _options.WeakLessonCount = 2;
        _options.WeakObjectiveCount = 1;
        var second = AddLesson("Energy", 2);
        var third = AddLesson("Momentum", 3);
        _builder.Lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(_builder.ObjectiveId, "State the first law"), new LessonObjectiveContent(null, "State the second law"), new LessonObjectiveContent(null, "State the third law")], Guid.NewGuid());
        var objectives = _builder.Lesson.Objectives;
        StubLessonCounts([LessonCount(_builder.Lesson, 4, 1, 1), LessonCount(second, 4, 2, 2), LessonCount(third, 4, 3, 3)]);
        StubObjectiveCounts([ObjectiveCount(objectives[0].Id, 1, 4, 1, 1), ObjectiveCount(objectives[1].Id, 2, 4, 2, 2), ObjectiveCount(objectives[2].Id, 3, 4, 3, 3)]);

        var result = await _handler.Handle(new GetWeakSpotsQuery(), TestContext.Current.CancellationToken);

        result.Lessons.Select(x => x.LessonId).Should().Equal(_builder.Lesson.Id, second.Id);
        result.Objectives.Should().ContainSingle().Which.ObjectiveId.Should().Be(objectives[0].Id);
    }

    private Lesson AddLesson(string name, int order)
    {
        var lesson = Lesson.Create(_builder.Unit, name, order, Guid.NewGuid());
        _lessons.Add(lesson);
        return lesson;
    }

    private LessonMasteryCount LessonCount(Lesson lesson, int servable, int mastered, int seen) => new(_builder.Subject.Id, 1, _builder.Unit.Id, 1, lesson.Id, lesson.Order, servable, mastered, seen);

    private ObjectiveMasteryCount ObjectiveCount(Guid objectiveId, int objectiveOrder, int servable, int mastered, int seen) => new(_builder.Subject.Id, 1, _builder.Unit.Id, 1, _builder.Lesson.Id, 1, objectiveId, objectiveOrder, servable, mastered, seen);

    private void StubLessonCounts(List<LessonMasteryCount> counts) => _questionMasteryRepository.GetLessonCountsAsync(_studentId, null, Arg.Any<CancellationToken>()).Returns(counts);

    private void StubObjectiveCounts(List<ObjectiveMasteryCount> counts) => _questionMasteryRepository.GetObjectiveCountsAsync(_studentId, Arg.Any<CancellationToken>()).Returns(counts);
}
