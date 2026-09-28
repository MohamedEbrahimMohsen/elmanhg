using Core.DDD.Models;
using Elmanhg.Application.Questions.GetQuestions;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Questions.GetQuestions;

public sealed class GetQuestionsHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly QuestionBuilder _builder = new();
    private readonly GetQuestionsHandler _handler;

    public GetQuestionsHandlerTests()
    {
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns([_builder.Lesson]);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([_builder.Teacher]);
        _handler = new GetQuestionsHandler(_questionRepository, _lessonRepository, _userRepository);
    }

    [Fact]
    public async Task Handle_Page_MapsLessonAndTeacherNamesAndPaging()
    {
        ReturnsPage(_builder.Rejected("Wrong unit").Build());

        var page = await _handler.Handle(new GetQuestionsQuery(null, null, null, null, null, null, null), TestContext.Current.CancellationToken);

        var item = page.Items.Should().ContainSingle().Subject;
        item.LessonName.Should().Be("Newton's laws");
        item.TeacherName.Should().Be("Teacher");
        item.RejectionReason.Should().Be("Wrong unit");
        item.Type.Should().Be("Mcq");
        item.ValidationStatus.Should().Be("Rejected");
        page.PageNumber.Should().Be(1);
        page.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UndecidedQuestion_HasNoTeacherName()
    {
        ReturnsPage(_builder.Build());

        var page = await _handler.Handle(new GetQuestionsQuery(null, null, null, null, null, null, null), TestContext.Current.CancellationToken);

        var item = page.Items.Should().ContainSingle().Subject;
        item.TeacherName.Should().BeNull();
        item.ValidatedBy.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ApprovedInPublishedLesson_IsServable()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        ReturnsPage(_builder.Approved().Build());

        var page = await _handler.Handle(new GetQuestionsQuery(null, null, null, null, null, null, null), TestContext.Current.CancellationToken);

        var item = page.Items.Should().ContainSingle().Subject;
        item.IsServable.Should().BeTrue();
        item.RetiredAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PendingQuestion_IsListedAndNotServable()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        ReturnsPage(_builder.Build());

        var page = await _handler.Handle(new GetQuestionsQuery(null, null, null, null, null, null, null), TestContext.Current.CancellationToken);

        var item = page.Items.Should().ContainSingle().Subject;
        item.IsServable.Should().BeFalse();
        item.ValidationStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_RetiredQuestion_IsListedWithRetiredAtAndNotServable()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        ReturnsPage(_builder.Approved().Retired().Build());

        var page = await _handler.Handle(new GetQuestionsQuery(null, null, null, null, null, null, null), TestContext.Current.CancellationToken);

        var item = page.Items.Should().ContainSingle().Subject;
        item.RetiredAt.Should().NotBeNull();
        item.IsServable.Should().BeFalse();
    }

    private void ReturnsPage(Question question)
    {
        _questionRepository.FindPaginatedAsync(1, 20, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Question, bool>>?>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), true)
            .Returns(new PageData<Question> { Items = [question], PageNumber = 1, PageSize = 20, TotalItems = 1, TotalPages = 1 });
    }
}
