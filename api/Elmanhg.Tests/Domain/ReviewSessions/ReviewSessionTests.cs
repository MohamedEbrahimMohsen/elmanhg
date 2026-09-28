using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ReviewSessions;

public sealed class ReviewSessionTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(480);
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void Start_SetsTeacherCreatorAndExpiry()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);

        session.TeacherId.Should().Be(_teacherId);
        session.CreatedBy.Should().Be(_teacherId);
        session.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.Add(Lifetime), TimeSpan.FromSeconds(5));
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void RecordOpening_ActiveSession_AddsOpeningAtCurrentVersion()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);
        var question = _builder.Build();

        session.RecordOpening(question);

        var opening = session.Openings.Should().ContainSingle().Subject;
        opening.QuestionId.Should().Be(question.Id);
        opening.QuestionVersion.Should().Be(1);
        opening.ReviewSessionId.Should().Be(session.Id);
    }

    [Fact]
    public void RecordOpening_SameVersionTwice_AddsOnce()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);
        var question = _builder.Build();

        session.RecordOpening(question);
        session.RecordOpening(question);

        session.Openings.Should().HaveCount(1);
    }

    [Fact]
    public void RecordOpening_ExpiredSession_ThrowsReviewSessionExpired()
    {
        var session = ReviewSession.Start(_teacherId, TimeSpan.Zero);

        var act = () => session.RecordOpening(_builder.Build());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ReviewSessionExpired);
        session.Openings.Should().BeEmpty();
    }

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
