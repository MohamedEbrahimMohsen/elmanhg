using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.GetQuestion;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.GetQuestion;

public sealed class GetQuestionHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly GetQuestionHandler _handler;

    public GetQuestionHandlerTests()
    {
        _handler = new GetQuestionHandler(_questionRepository);
    }

    [Fact]
    public async Task Handle_ExistingQuestion_ReturnsDetailWithParsedJson()
    {
        var question = new QuestionBuilder().Build();
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);

        var result = await _handler.Handle(new GetQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        result.Type.Should().Be("Mcq");
        result.ValidationStatus.Should().Be("Pending");
        result.Body.GetProperty("options").GetArrayLength().Should().Be(2);
        result.GradingSpec.GetProperty("correctOptionId").GetString().Should().Be("b");
    }

    [Fact]
    public async Task Handle_RejectedQuestion_ReturnsRejectionReason()
    {
        var question = new QuestionBuilder().Rejected("Wrong unit").Build();
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);

        var result = await _handler.Handle(new GetQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        result.RejectionReason.Should().Be("Wrong unit");
        result.ValidationStatus.Should().Be("Rejected");
    }

    [Fact]
    public async Task Handle_RetiredQuestion_ReturnsRetiredAt()
    {
        var question = new QuestionBuilder().Retired().Build();
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);

        var result = await _handler.Handle(new GetQuestionQuery(question.Id), TestContext.Current.CancellationToken);

        result.RetiredAt.Should().NotBeNull().And.Be(question.RetiredAt);
    }

    [Fact]
    public async Task Handle_QuestionNotFound_ThrowsQuestionNotFound()
    {
        var act = () => _handler.Handle(new GetQuestionQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }
}
