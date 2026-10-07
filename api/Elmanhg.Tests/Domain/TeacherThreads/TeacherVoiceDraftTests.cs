using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherVoiceDraftTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(15);
    private readonly Guid _threadId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void Record_SetsPendingDueNowWithZeroAttempts()
    {
        var draft = TeacherVoiceDraft.Record(_threadId, _teacherId, "teacher-threads/a.webm", "/api/media/teacher-threads/a.webm", 42, RecordedAt.AddTicks(7));

        (draft.ThreadId, draft.TeacherId, draft.AudioKey, draft.AudioUrl, draft.AudioDurationSeconds, draft.CreatedBy).Should().Be((_threadId, _teacherId, "teacher-threads/a.webm", "/api/media/teacher-threads/a.webm", 42, (Guid?)_teacherId));
        (draft.Status, draft.Attempts, draft.Transcript, draft.TranscribedAt, draft.SentMessageId).Should().Be((TeacherVoiceDraftStatus.Pending, 0, (string?)null, (DateTimeOffset?)null, (Guid?)null));
        (draft.RecordedAt, draft.NextAttemptAt).Should().Be((RecordedAt, (DateTimeOffset?)RecordedAt));
    }

    [Fact]
    public void IsDueAt_PendingAtNextAttempt_ReturnsTrue()
    {
        var draft = Record();

        draft.IsDueAt(RecordedAt).Should().BeTrue();
    }

    [Fact]
    public void IsDueAt_PendingBeforeNextAttempt_ReturnsFalse()
    {
        var draft = Record();

        draft.IsDueAt(RecordedAt.AddTicks(-10)).Should().BeFalse();
    }

    [Fact]
    public void IsDueAt_Ready_ReturnsFalse()
    {
        var draft = Record();
        draft.CompleteTranscription("text", "whisper-1", RecordedAt.AddSeconds(5));

        draft.IsDueAt(RecordedAt.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void CompleteTranscription_Pending_SetsReadyTrimmedTranscriptAndModel()
    {
        var draft = Record();

        draft.CompleteTranscription("  The transcript.  ", "whisper-1", RecordedAt.AddSeconds(5).AddTicks(3));

        (draft.Status, draft.Transcript, draft.TranscriptionModel, draft.Attempts, draft.NextAttemptAt).Should().Be((TeacherVoiceDraftStatus.Ready, "The transcript.", "whisper-1", 1, (DateTimeOffset?)null));
        (draft.TranscribedAt, draft.UpdationDate).Should().Be(((DateTimeOffset?)RecordedAt.AddSeconds(5), RecordedAt.AddSeconds(5)));
    }

    [Fact]
    public void CompleteTranscription_NotPending_ThrowsNotPending()
    {
        var draft = Record();
        draft.CompleteTranscription("text", "whisper-1", RecordedAt.AddSeconds(5));

        var act = () => draft.CompleteTranscription("again", "whisper-1", RecordedAt.AddSeconds(9));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceDraftNotPending);
        draft.Transcript.Should().Be("text");
    }

    [Fact]
    public void FailAttempt_BelowMax_SchedulesExponentialRetry()
    {
        var draft = Record();

        draft.FailAttempt(RecordedAt.AddSeconds(1), 4, RetryBaseDelay);
        var firstRetry = (draft.Attempts, draft.NextAttemptAt);
        draft.FailAttempt(RecordedAt.AddSeconds(20), 4, RetryBaseDelay);

        firstRetry.Should().Be((1, (DateTimeOffset?)RecordedAt.AddSeconds(16)));
        (draft.Attempts, draft.NextAttemptAt, draft.Status, draft.UpdationDate).Should().Be((2, (DateTimeOffset?)RecordedAt.AddSeconds(50), TeacherVoiceDraftStatus.Pending, RecordedAt.AddSeconds(20)));
    }

    [Fact]
    public void FailAttempt_ReachingMax_MarksFailed()
    {
        var draft = Record();
        draft.FailAttempt(RecordedAt.AddSeconds(1), 2, RetryBaseDelay);

        draft.FailAttempt(RecordedAt.AddSeconds(20), 2, RetryBaseDelay);

        (draft.Attempts, draft.Status, draft.NextAttemptAt).Should().Be((2, TeacherVoiceDraftStatus.Failed, (DateTimeOffset?)null));
    }

    [Fact]
    public void FailAttempt_NotPending_ThrowsNotPending()
    {
        var draft = Record();
        draft.FailAttempt(RecordedAt.AddSeconds(1), 1, RetryBaseDelay);

        var act = () => draft.FailAttempt(RecordedAt.AddSeconds(20), 1, RetryBaseDelay);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceDraftNotPending);
        draft.Attempts.Should().Be(1);
    }

    [Fact]
    public void MarkSent_Ready_SetsSentWithMessageId()
    {
        var draft = Record();
        draft.CompleteTranscription("text", "whisper-1", RecordedAt.AddSeconds(5));
        draft.UpdatedBy = null;
        var messageId = Guid.NewGuid();

        draft.MarkSent(messageId, RecordedAt.AddMinutes(1));

        (draft.Status, draft.SentMessageId, draft.UpdatedBy, draft.UpdationDate).Should().Be((TeacherVoiceDraftStatus.Sent, (Guid?)messageId, (Guid?)null, RecordedAt.AddMinutes(1)));
    }

    [Fact]
    public void MarkSent_Failed_SetsSent()
    {
        var draft = Record();
        draft.FailAttempt(RecordedAt.AddSeconds(1), 1, RetryBaseDelay);

        draft.MarkSent(Guid.NewGuid(), RecordedAt.AddMinutes(1));

        draft.Status.Should().Be(TeacherVoiceDraftStatus.Sent);
    }

    [Fact]
    public void MarkSent_Pending_ThrowsNotReady()
    {
        var draft = Record();

        var act = () => draft.MarkSent(Guid.NewGuid(), RecordedAt.AddMinutes(1));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceDraftNotReady);
        draft.Status.Should().Be(TeacherVoiceDraftStatus.Pending);
    }

    [Fact]
    public void MarkSent_AlreadySent_ThrowsAlreadySent()
    {
        var draft = Record();
        draft.CompleteTranscription("text", "whisper-1", RecordedAt.AddSeconds(5));
        var firstMessageId = Guid.NewGuid();
        draft.MarkSent(firstMessageId, RecordedAt.AddMinutes(1));

        var act = () => draft.MarkSent(Guid.NewGuid(), RecordedAt.AddMinutes(2));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceDraftAlreadySent);
        draft.SentMessageId.Should().Be(firstMessageId);
    }

    private TeacherVoiceDraft Record() => TeacherVoiceDraft.Record(_threadId, _teacherId, "teacher-threads/a.webm", "/api/media/teacher-threads/a.webm", 42, RecordedAt);
}
