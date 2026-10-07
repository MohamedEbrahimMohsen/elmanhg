using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ReviewSessions;

public sealed class ReviewSessionOpeningCheckTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(480);
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void HasOpened_AfterContentEdit_ReturnsFalse()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);
        var question = _builder.Build();
        session.RecordOpening(question);

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _builder.Lesson, Guid.NewGuid());

        session.HasOpened(question).Should().BeFalse();
    }

    [Fact]
    public void EnsureOpened_Opened_DoesNotThrow()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);
        var question = _builder.Build();
        session.RecordOpening(question);

        var act = () => session.EnsureOpened(question);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureOpened_NotOpened_ThrowsWithQuestionIdContext()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);
        var question = _builder.Build();

        var act = () => session.EnsureOpened(question);

        var exception = act.Should().Throw<BusinessRuleViolationCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.QuestionNotOpenedInSession);
        exception.Context!["questionId"].Should().Be(question.Id);
    }

    [Fact]
    public void EnsureOpened_ExpiredSession_ThrowsReviewSessionExpired()
    {
        var session = ReviewSession.Start(_teacherId, TimeSpan.Zero);

        var act = () => session.EnsureOpened(_builder.Build());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ReviewSessionExpired);
    }
}
