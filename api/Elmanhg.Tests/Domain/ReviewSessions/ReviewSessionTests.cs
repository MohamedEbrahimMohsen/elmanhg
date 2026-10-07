using Core.Errors;
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
    public void RecordOpening_ActiveSession_LeavesUpdatedByToTheSave()
    {
        var session = ReviewSession.Start(_teacherId, Lifetime);
        session.UpdatedBy = null;

        session.RecordOpening(_builder.Build());

        session.UpdatedBy.Should().BeNull();
        session.Openings.Should().ContainSingle();
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
}
