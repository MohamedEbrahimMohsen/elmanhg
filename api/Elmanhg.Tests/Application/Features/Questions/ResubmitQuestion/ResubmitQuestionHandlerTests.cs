using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.ResubmitQuestion;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Questions.ResubmitQuestion;

public sealed class ResubmitQuestionHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly ResubmitQuestionHandler _handler;

    public ResubmitQuestionHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => x.Arg<string?>() ?? string.Empty);
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_builder.Lesson);
        _handler = new ResubmitQuestionHandler(_questionRepository, _lessonRepository, _richTextSanitizer, _currentUserService);
    }

    [Fact]
    public async Task Handle_RejectedQuestion_ReturnsToPendingAndSaves()
    {
        var question = Returns(_builder.Rejected("Wrong unit").Build());

        await _handler.Handle(new ResubmitQuestionCommand(question.Id, QuestionBuilder.McqFields("<p>3 + 3 = ?</p>")), TestContext.Current.CancellationToken);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.RejectionReason.Should().BeNull();
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionNotFound_ThrowsQuestionNotFound()
    {
        var act = () => _handler.Handle(new ResubmitQuestionCommand(Guid.NewGuid(), QuestionBuilder.McqFields()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var question = Returns(_builder.Rejected("Wrong unit").Build());
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns((Lesson?)null);

        var act = () => _handler.Handle(new ResubmitQuestionCommand(question.Id, QuestionBuilder.McqFields()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingQuestion_ThrowsQuestionNotRejected()
    {
        var question = Returns(_builder.Build());

        var act = () => _handler.Handle(new ResubmitQuestionCommand(question.Id, QuestionBuilder.McqFields()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.QuestionNotRejected);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var question = Returns(_builder.Rejected("Wrong unit").Build());
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new ResubmitQuestionCommand(question.Id, QuestionBuilder.McqFields()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DragDropImageFromAnotherLesson_ThrowsQuestionDiagramImageInvalid()
    {
        var question = Returns(_builder.DragDrop().Rejected("Wrong unit").Build());
        var fields = QuestionBuilder.DragDropFields() with { Body = QuestionBuilder.Json(QuestionBuilder.ForLesson(QuestionBuilder.DragDropBodyJson, Guid.NewGuid())) };

        var act = () => _handler.Handle(new ResubmitQuestionCommand(question.Id, fields), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ApplicationValidationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionDiagramImageInvalid);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Question Returns(Question question)
    {
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        return question;
    }
}
