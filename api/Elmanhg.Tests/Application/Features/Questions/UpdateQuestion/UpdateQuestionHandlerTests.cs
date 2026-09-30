using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.UpdateQuestion;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Application.Features.Questions.Shared;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Questions.UpdateQuestion;

public sealed class UpdateQuestionHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly Question _question;
    private readonly UpdateQuestionHandler _handler;

    public UpdateQuestionHandlerTests()
    {
        _question = _builder.Approved().Build();
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => x.Arg<string?>() ?? string.Empty);
        _questionRepository.GetByIdAsync(_question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(_question);
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_builder.Lesson);
        _handler = new UpdateQuestionHandler(_questionRepository, _lessonRepository, _richTextSanitizer, _currentUserService);
    }

    [Fact]
    public async Task Handle_ContentEditOnApproved_ResetsToPendingBumpsVersionAndSaves()
    {
        var fields = Unchanged() with { Stem = "<p>3 + 3 = ?</p>" };

        await _handler.Handle(new UpdateQuestionCommand(_question.Id, fields), TestContext.Current.CancellationToken);

        _question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        _question.Version.Should().Be(2);
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MetadataEditOnApproved_KeepsApprovedAndSaves()
    {
        await _handler.Handle(new UpdateQuestionCommand(_question.Id, Unchanged() with { Difficulty = QuestionDifficulty.Hard }), TestContext.Current.CancellationToken);

        _question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        _question.Version.Should().Be(1);
        _question.Difficulty.Should().Be(QuestionDifficulty.Hard);
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionNotFound_ThrowsQuestionNotFound()
    {
        var act = () => _handler.Handle(new UpdateQuestionCommand(Guid.NewGuid(), Unchanged()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns((Lesson?)null);

        var act = () => _handler.Handle(new UpdateQuestionCommand(_question.Id, Unchanged()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TypeChanged_ThrowsQuestionTypeImmutable()
    {
        var fields = Unchanged() with { Type = QuestionType.Multi, GradingSpec = QuestionBuilder.Json("""{"correctOptionIds":["b"]}""") };

        var act = () => _handler.Handle(new UpdateQuestionCommand(_question.Id, fields), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.QuestionTypeImmutable);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DragDropImageFromAnotherLesson_ThrowsQuestionDiagramImageInvalid()
    {
        var question = _builder.DragDrop().Build();
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        var fields = QuestionBuilder.DragDropFields() with { Body = QuestionBuilder.Json(QuestionBuilder.ForLesson(QuestionBuilder.DragDropBodyJson, Guid.NewGuid())) };

        var act = () => _handler.Handle(new UpdateQuestionCommand(question.Id, fields), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ApplicationValidationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionDiagramImageInvalid);
        question.Version.Should().Be(1);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UpdateQuestionCommand(_question.Id, Unchanged()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static QuestionFields Unchanged()
    {
        return QuestionFieldsValidatorTests.ValidMcq() with { Explanation = "<p>Add the numbers.</p>", Tags = [] };
    }
}
