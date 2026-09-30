using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.CreateQuestion;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Questions.Shared;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using static Elmanhg.Tests.Builders.QuestionBuilder;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Questions.CreateQuestion;

public sealed class CreateQuestionHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly CreateQuestionHandler _handler;

    public CreateQuestionHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => $"clean:{x.Arg<string?>()}");
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_builder.Lesson);
        _unitRepository.GetByIdAsync(_builder.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_builder.Unit);
        _handler = new CreateQuestionHandler(_lessonRepository, _unitRepository, _questionRepository, _richTextSanitizer, _currentUserService);
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsPendingQuestionAndSaves()
    {
        Question? added = null;
        await _questionRepository.AddAsync(Arg.Do<Question>(x => added = x), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new CreateQuestionCommand(_builder.Lesson.Id, QuestionFieldsValidatorTests.ValidMcq()), TestContext.Current.CancellationToken);

        added.Should().NotBeNull();
        added!.SubjectId.Should().Be(_builder.Unit.SubjectId);
        added.Stem.Should().Be("clean:<p>2 + 2 = ?</p>");
        added.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        added.Version.Should().Be(1);
        result.Id.Should().Be(added.Id);
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new CreateQuestionCommand(Guid.NewGuid(), QuestionFieldsValidatorTests.ValidMcq()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitNotFound_ThrowsUnitNotFound()
    {
        _unitRepository.GetByIdAsync(_builder.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns((CurriculumUnit?)null);

        var act = () => _handler.Handle(new CreateQuestionCommand(_builder.Lesson.Id, QuestionFieldsValidatorTests.ValidMcq()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ObjectiveNotInLesson_ThrowsQuestionObjectiveNotInLesson()
    {
        var command = new CreateQuestionCommand(_builder.Lesson.Id, QuestionFieldsValidatorTests.ValidMcq() with { ObjectiveId = Guid.NewGuid() });

        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.QuestionObjectiveNotInLesson);
        await _questionRepository.DidNotReceive().AddAsync(Arg.Any<Question>(), Arg.Any<CancellationToken>());
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DragDropImageInLesson_AddsQuestion()
    {
        var fields = DragDropFields() with { Body = Json(ForLesson(DragDropBodyJson, _builder.Lesson.Id)) };

        await _handler.Handle(new CreateQuestionCommand(_builder.Lesson.Id, fields), TestContext.Current.CancellationToken);

        await _questionRepository.Received(1).AddAsync(Arg.Is<Question>(x => x.Type == QuestionType.DragDrop), Arg.Any<CancellationToken>());
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DragDropImageFromAnotherLesson_ThrowsQuestionDiagramImageInvalid()
    {
        var fields = DragDropFields() with { Body = Json(ForLesson(DragDropBodyJson, Guid.NewGuid())) };

        var act = () => _handler.Handle(new CreateQuestionCommand(_builder.Lesson.Id, fields), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ApplicationValidationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionDiagramImageInvalid);
        await _questionRepository.DidNotReceive().AddAsync(Arg.Any<Question>(), Arg.Any<CancellationToken>());
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new CreateQuestionCommand(_builder.Lesson.Id, QuestionFieldsValidatorTests.ValidMcq()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
