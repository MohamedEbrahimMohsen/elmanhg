using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetSessionHistory;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Progress.GetSessionHistory;

public sealed class GetSessionHistoryHandlerTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<Lesson> _lessons = [];
    private readonly GetSessionHistoryHandler _handler;

    public GetSessionHistoryHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _lessons.Add(_builder.Questions.Lesson);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _handler = new GetSessionHistoryHandler(_sessionRepository, _lessonRepository, _unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetSessionHistoryQuery(null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _sessionRepository.DidNotReceive().FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_QuizSessions_ReturnsLessonNameScoreAndPaging()
    {
        var open = _builder.Build();
        var finished = _builder.Build();
        finished.RecordAttempt(finished.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        finished.Submit();
        StubPage([open, finished], pageNumber: 2, totalItems: 12, totalPages: 3);

        var result = await _handler.Handle(new GetSessionHistoryQuery(null, 2, 5), TestContext.Current.CancellationToken);

        result.Items.Select(x => x.Id).Should().Equal(open.Id, finished.Id);
        result.Items.Should().AllSatisfy(x => (x.Kind, x.LessonId, x.UnitId, x.ScopeName).Should().Be(("Quiz", _builder.Questions.Lesson.Id, (Guid?)null, "Newton's laws")));
        (result.Items[0].SubmittedAt, result.Items[0].ScorePercent).Should().Be(((DateTimeOffset?)null, (decimal?)null));
        (result.Items[1].SubmittedAt, result.Items[1].ScorePercent, result.Items[1].StartedAt).Should().Be((finished.SubmittedAt, 50.00m, finished.StartedAt));
        (result.PageNumber, result.PageSize, result.TotalItems, result.TotalPages).Should().Be((2L, 5L, 12L, 3L));
    }

    [Fact]
    public async Task Handle_LessonDeleted_ReturnsNullScopeName()
    {
        _lessons.Clear();
        var session = _builder.Build();
        StubPage([session], pageNumber: 1, totalItems: 1, totalPages: 1);

        var result = await _handler.Handle(new GetSessionHistoryQuery(null), TestContext.Current.CancellationToken);

        var item = result.Items.Should().ContainSingle().Subject;
        (item.LessonId, item.ScopeName).Should().Be((_builder.Questions.Lesson.Id, (string?)null));
    }

    [Fact]
    public async Task Handle_EmptyPage_DoesNotLoadLessonsOrUnits()
    {
        StubPage([], pageNumber: 1, totalItems: 0, totalPages: 0);

        var result = await _handler.Handle(new GetSessionHistoryQuery(SessionHistoryKind.Exam), TestContext.Current.CancellationToken);

        result.Items.Should().BeEmpty();
        await _lessonRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>());
        await _unitRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>());
    }

    private void StubPage(List<Session> sessions, int pageNumber, int totalItems, int totalPages)
    {
        _sessionRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call => new PageData<Session> { Items = sessions, PageNumber = pageNumber, PageSize = call.ArgAt<int>(1), TotalItems = totalItems, TotalPages = totalPages });
    }
}
