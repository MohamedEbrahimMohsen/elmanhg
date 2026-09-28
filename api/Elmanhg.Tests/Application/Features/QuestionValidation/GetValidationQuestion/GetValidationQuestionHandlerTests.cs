using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.GetValidationQuestion;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.GetValidationQuestion;

public sealed class GetValidationQuestionHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly GetValidationQuestionHandler _handler;

    public GetValidationQuestionHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.Teacher.Id);
        _teacherSubjectRepository.IsAssignedAsync(_builder.Teacher.Id, _builder.Subject.Id, Arg.Any<CancellationToken>()).Returns(true);
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_builder.Lesson);
        _unitRepository.GetByIdAsync(_builder.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_builder.Unit);
        _subjectRepository.GetByIdAsync(_builder.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Subject);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([_builder.Teacher]);
        _handler = new GetValidationQuestionHandler(_questionRepository, _teacherSubjectRepository, _lessonRepository, _unitRepository, _subjectRepository, _userRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_AssignedTeacher_ReturnsDetailWithHistoryAndObjective()
    {
        var question = Stub(_builder.WithMetadata(new QuestionMetadata(QuestionDifficulty.Medium, _builder.ObjectiveId, [])).Rejected("Wrong unit").Build());

        var result = await _handler.Handle(new GetValidationQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        result.Revisions.Should().ContainSingle().Which.Version.Should().Be(1);
        var decision = result.Decisions.Should().ContainSingle().Subject;
        decision.Outcome.Should().Be("Rejected");
        decision.Reason.Should().Be("Wrong unit");
        decision.DecidedByName.Should().Be("Teacher");
        result.ObjectiveText.Should().Be("State the first law");
        result.LessonState.Should().Be("Draft");
        result.SubjectName.Should().Be("Physics");
        result.UnitName.Should().Be("Mechanics");
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var question = Stub(_builder.Build());
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetValidationQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_QuestionMissing_ThrowsQuestionNotFound()
    {
        var act = () => _handler.Handle(new GetValidationQuestionQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task Handle_OutOfScope_ThrowsSubjectOutOfScope()
    {
        var question = Stub(_builder.Build());
        _teacherSubjectRepository.IsAssignedAsync(_builder.Teacher.Id, _builder.Subject.Id, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _handler.Handle(new GetValidationQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
    }

    [Fact]
    public async Task Handle_LessonMissing_ThrowsLessonNotFound()
    {
        var question = Stub(_builder.Build());
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns((Lesson?)null);

        var act = () => _handler.Handle(new GetValidationQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    private Question Stub(Question question)
    {
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        return question;
    }
}
