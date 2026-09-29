using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadClaimAndReplyTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _otherTeacherId = Guid.NewGuid();

    [Fact]
    public void Claim_UnclaimedThread_SetsTeacherAndClaimedAt()
    {
        var thread = new TeacherThreadBuilder().Build();

        thread.Claim(_teacherId, SubmittedAt.AddMinutes(5).AddTicks(7));

        (thread.TeacherId, thread.ClaimedAt, thread.UpdatedBy, thread.UpdationDate).Should().Be(((Guid?)_teacherId, (DateTimeOffset?)SubmittedAt.AddMinutes(5), (Guid?)_teacherId, SubmittedAt.AddMinutes(5)));
        thread.Status.Should().Be(TeacherThreadStatus.Open);
    }

    [Fact]
    public void Claim_SameTeacherAgain_KeepsFirstClaim()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();
        var firstClaim = thread.ClaimedAt;

        thread.Claim(_teacherId, SubmittedAt.AddHours(2));

        (thread.TeacherId, thread.ClaimedAt).Should().Be(((Guid?)_teacherId, firstClaim));
    }

    [Fact]
    public void Claim_ClaimedByAnotherTeacher_ThrowsAlreadyClaimed()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_otherTeacherId).Build();

        var act = () => thread.Claim(_teacherId, SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadAlreadyClaimed);
        thread.TeacherId.Should().Be(_otherTeacherId);
    }

    [Fact]
    public void Reply_ClaimedOpenThread_AddsTextReplyAndMarksAnswered()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var message = thread.Reply(_teacherId, "  Because F = ma.  ", SubmittedAt.AddHours(2));

        (message.SenderId, message.Kind, message.Text, message.ThreadId, message.CreatedAt).Should().Be((_teacherId, TeacherMessageKind.Text, "Because F = ma.", thread.Id, SubmittedAt.AddHours(2)));
        thread.Messages.Should().HaveCount(2);
        (thread.Status, thread.UpdationDate).Should().Be((TeacherThreadStatus.Answered, SubmittedAt.AddHours(2)));
    }

    [Fact]
    public void Reply_UnclaimedThread_ThrowsNotClaimed()
    {
        var thread = new TeacherThreadBuilder().Build();

        var act = () => thread.Reply(_teacherId, "Because F = ma.", SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotClaimed);
        thread.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void Reply_ClaimedByAnotherTeacher_ThrowsAlreadyClaimed()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_otherTeacherId).Build();

        var act = () => thread.Reply(_teacherId, "Because F = ma.", SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadAlreadyClaimed);
    }

    [Fact]
    public void Reply_AnsweredThread_ThrowsNotAwaitingReply()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        var act = () => thread.Reply(_teacherId, "One more thing.", SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotAwaitingReply);
    }

    [Fact]
    public void Reply_BlankText_ThrowsTeacherMessageTextRequired()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var act = () => thread.Reply(_teacherId, "   ", SubmittedAt.AddHours(2));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherMessageTextRequired);
        thread.Status.Should().Be(TeacherThreadStatus.Open);
    }

    [Fact]
    public void HasUnreadReply_OnlyStudentMessage_ReturnsFalse()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        thread.HasUnreadReply().Should().BeFalse();
    }

    [Fact]
    public void HasUnreadReply_AfterReply_ReturnsTrue()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        thread.HasUnreadReply().Should().BeTrue();
    }

    [Fact]
    public void MarkRepliesRead_AfterReply_StampsTeacherMessagesOnly()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        thread.MarkRepliesRead(SubmittedAt.AddHours(3).AddTicks(7));

        thread.Messages.Single(x => x.SenderId == _teacherId).StudentReadAt.Should().Be(SubmittedAt.AddHours(3));
        thread.Messages.Single(x => x.SenderId == thread.StudentId).StudentReadAt.Should().BeNull();
        thread.HasUnreadReply().Should().BeFalse();
    }

    [Fact]
    public void MarkRepliesRead_AlreadyRead_KeepsFirstReadTime()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();
        thread.MarkRepliesRead(SubmittedAt.AddHours(3));

        thread.MarkRepliesRead(SubmittedAt.AddHours(5));

        thread.Messages.Single(x => x.SenderId == _teacherId).StudentReadAt.Should().Be(SubmittedAt.AddHours(3));
    }
}
