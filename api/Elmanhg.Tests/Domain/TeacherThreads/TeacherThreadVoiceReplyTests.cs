using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadVoiceReplyTests
{
    private const string AudioUrl = "/api/media/teacher-threads/voice.webm";
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void ReplyWithVoice_ClaimerOnOpenThread_AddsVoiceMessageAndAnswers()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var message = thread.ReplyWithVoice(_teacherId, "  The transcript.  ", AudioUrl, 42, SubmittedAt.AddHours(2).AddTicks(7));

        (message.Kind, message.Text, message.AudioUrl, message.AudioDurationSeconds, message.TranscriptFinal, message.CreatedAt).Should().Be((TeacherMessageKind.Voice, "The transcript.", AudioUrl, (int?)42, true, SubmittedAt.AddHours(2)));
        thread.Messages.Should().HaveCount(2).And.Contain(message);
        (thread.Status, thread.UpdatedBy, thread.UpdationDate).Should().Be((TeacherThreadStatus.Answered, (Guid?)_teacherId, SubmittedAt.AddHours(2)));
    }

    [Fact]
    public void ReplyWithVoice_Unclaimed_ThrowsNotClaimed()
    {
        var thread = new TeacherThreadBuilder().Build();

        var act = () => thread.ReplyWithVoice(_teacherId, "The transcript.", AudioUrl, 42, SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotClaimed);
        thread.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void ReplyWithVoice_ClaimedByOther_ThrowsAlreadyClaimed()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(Guid.NewGuid()).Build();

        var act = () => thread.ReplyWithVoice(_teacherId, "The transcript.", AudioUrl, 42, SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadAlreadyClaimed);
    }

    [Fact]
    public void ReplyWithVoice_Answered_ThrowsNotAwaitingReply()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        var act = () => thread.ReplyWithVoice(_teacherId, "The transcript.", AudioUrl, 42, SubmittedAt.AddHours(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotAwaitingReply);
    }

    [Fact]
    public void ReplyWithVoice_BlankText_ThrowsTextRequired()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var act = () => thread.ReplyWithVoice(_teacherId, "   ", AudioUrl, 42, SubmittedAt.AddHours(2));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherMessageTextRequired);
        thread.Status.Should().Be(TeacherThreadStatus.Open);
    }

    [Fact]
    public void EnsureCanReply_ClaimerOnOpenThread_DoesNotThrow()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var act = () => thread.EnsureCanReply(_teacherId);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanReply_Unclaimed_ThrowsNotClaimed()
    {
        var thread = new TeacherThreadBuilder().Build();

        var act = () => thread.EnsureCanReply(_teacherId);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotClaimed);
    }

    [Fact]
    public void EnsureCanReply_Answered_ThrowsNotAwaitingReply()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        var act = () => thread.EnsureCanReply(_teacherId);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotAwaitingReply);
    }

    [Fact]
    public void Reply_Text_HasNoAudioAndTranscriptNotFinal()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var message = thread.Reply(_teacherId, "Because F = ma.", SubmittedAt.AddHours(2));

        (message.AudioUrl, message.AudioDurationSeconds, message.TranscriptFinal).Should().Be(((string?)null, (int?)null, false));
    }
}
