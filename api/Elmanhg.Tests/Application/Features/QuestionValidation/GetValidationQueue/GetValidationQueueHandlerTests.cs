using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.GetValidationQueue;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.GetValidationQueue;

public sealed class GetValidationQueueHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IReviewSessionRepository _reviewSessionRepository = Substitute.For<IReviewSessionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly GetValidationQueueHandler _handler;

    public GetValidationQueueHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.Teacher.Id);
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns([TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid())]);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns([_builder.Lesson]);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([_builder.Unit]);
        _handler = new GetValidationQueueHandler(_questionRepository, _lessonRepository, _unitRepository, _teacherSubjectRepository, _reviewSessionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Teacher_ReturnsItemsWithLessonAndUnitNames()
    {
        var question = _builder.Build();
        ReturnsPage(question);

        var page = await _handler.Handle(Query(null), TestContext.Current.CancellationToken);

        var item = page.Items.Should().ContainSingle().Subject;
        item.LessonName.Should().Be("Newton's laws");
        item.UnitName.Should().Be("Mechanics");
        item.UnitId.Should().Be(_builder.Unit.Id);
        item.SubmittedAt.Should().Be(question.SubmittedAt);
        item.OpenedInSession.Should().BeFalse();
        page.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithOwnReviewSession_MarksOpenedItems()
    {
        var opened = _builder.Build();
        var unopened = _builder.Build();
        ReturnsPage(opened, unopened);
        var session = ReviewSession.Start(_builder.Teacher.Id, TimeSpan.FromHours(1));
        session.RecordOpening(opened);
        StubSession(session);

        var page = await _handler.Handle(Query(session.Id), TestContext.Current.CancellationToken);

        page.Items.Single(x => x.Id == opened.Id).OpenedInSession.Should().BeTrue();
        page.Items.Single(x => x.Id == unopened.Id).OpenedInSession.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReviewSessionOfOtherTeacher_ThrowsReviewSessionNotFound()
    {
        var session = ReviewSession.Start(Guid.NewGuid(), TimeSpan.FromHours(1));
        StubSession(session);

        var act = () => _handler.Handle(Query(session.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ReviewSessionNotFound);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(Query(null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    private static GetValidationQueueQuery Query(Guid? reviewSessionId) => new(null, null, null, null, null, reviewSessionId);

    private void StubSession(ReviewSession session)
    {
        _reviewSessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ReviewSession>, IQueryable<ReviewSession>>?>(), Arg.Any<bool>()).Returns(session);
    }

    private void ReturnsPage(params Question[] questions)
    {
        _questionRepository.FindPaginatedAsync(1, 20, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Question, bool>>?>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), true)
            .Returns(new PageData<Question> { Items = [.. questions], PageNumber = 1, PageSize = 20, TotalItems = questions.Length, TotalPages = 1 });
    }
}
