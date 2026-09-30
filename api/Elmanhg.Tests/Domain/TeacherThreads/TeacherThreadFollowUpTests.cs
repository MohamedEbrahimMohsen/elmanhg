using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadFollowUpTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private static readonly TimeSpan ReplySla = TimeSpan.FromHours(24);
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void FollowUp_AnsweredThread_AddsStudentMessageReopensAndResetsSla()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();
        var askedAt = SubmittedAt.AddHours(5);

        thread.FollowUp("Can you show the units?", askedAt.AddTicks(7), ReplySla);

        (thread.Status, thread.SlaDueAt, thread.UpdationDate, thread.UpdatedBy).Should().Be((TeacherThreadStatus.Open, askedAt + ReplySla, askedAt, (Guid?)thread.StudentId));
        thread.Messages.Should().HaveCount(3);
        (thread.Messages.Last().SenderId, thread.Messages.Last().Text, thread.Messages.Last().CreatedAt).Should().Be((thread.StudentId, "Can you show the units?", askedAt));
    }

    [Fact]
    public void FollowUp_OpenThread_ThrowsFollowUpNotAllowed()
    {
        ShouldRejectFollowUp(new TeacherThreadBuilder().ClaimedBy(_teacherId).Build());
    }

    [Fact]
    public void FollowUp_ClosedThread_ThrowsFollowUpNotAllowed()
    {
        ShouldRejectFollowUp(new TeacherThreadBuilder().AnsweredBy(_teacherId).FinalReplied().Build());
    }

    [Fact]
    public void FollowUp_AlreadyFollowedUp_ThrowsFollowUpNotAllowed()
    {
        ShouldRejectFollowUp(new TeacherThreadBuilder().AnsweredBy(_teacherId).FollowedUp().Build());
    }

    [Fact]
    public void FollowUp_BlankText_ThrowsTextRequired()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        var act = () => thread.FollowUp("   ", SubmittedAt.AddHours(5), ReplySla);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherMessageTextRequired);
        thread.Messages.Should().HaveCount(2);
        thread.Status.Should().Be(TeacherThreadStatus.Answered);
    }

    [Fact]
    public void Reply_AfterFollowUp_ClosesThreadAndSetsClosedAt()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FollowedUp().Build();
        var repliedAt = SubmittedAt.AddHours(6);

        thread.Reply(_teacherId, "Newtons.", repliedAt);

        (thread.Status, thread.ClosedAt).Should().Be((TeacherThreadStatus.Closed, (DateTimeOffset?)repliedAt));
    }

    [Fact]
    public void ReplyWithVoice_AfterFollowUp_ClosesThread()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FollowedUp().Build();
        var repliedAt = SubmittedAt.AddHours(6);

        thread.ReplyWithVoice(_teacherId, "Newtons.", "/api/media/teacher-threads/voice.webm", 12, repliedAt);

        (thread.Status, thread.ClosedAt).Should().Be((TeacherThreadStatus.Closed, (DateTimeOffset?)repliedAt));
    }

    [Fact]
    public void Reply_FirstReply_LeavesThreadAnsweredWithoutClosedAt()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        thread.Reply(_teacherId, "Because F = ma.", SubmittedAt.AddHours(1));

        (thread.Status, thread.ClosedAt).Should().Be((TeacherThreadStatus.Answered, (DateTimeOffset?)null));
    }

    [Fact]
    public void Reply_ClosedThread_ThrowsNotAwaitingReply()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FinalReplied().Build();

        var act = () => thread.Reply(_teacherId, "One more thing.", SubmittedAt.AddHours(7));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotAwaitingReply);
    }

    private static void ShouldRejectFollowUp(TeacherThread thread)
    {
        var status = thread.Status;
        var messageCount = thread.Messages.Count;

        var act = () => thread.FollowUp("Can you show the units?", SubmittedAt.AddHours(8), ReplySla);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadFollowUpNotAllowed);
        (thread.Status, thread.Messages.Count).Should().Be((status, messageCount));
    }
}
