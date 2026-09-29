using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.TeacherThreads;

public class TeacherVoiceDraft : AuditEntity
{
    public Guid ThreadId { get; private set; }
    public Guid TeacherId { get; private set; }
    public string AudioKey { get; private set; } = string.Empty;
    public string AudioUrl { get; private set; } = string.Empty;
    public int AudioDurationSeconds { get; private set; }
    public TeacherVoiceDraftStatus Status { get; private set; }
    public string? Transcript { get; private set; }
    public string? TranscriptionModel { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset? TranscribedAt { get; private set; }
    public Guid? SentMessageId { get; private set; }

    private TeacherVoiceDraft(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TeacherVoiceDraft Record(Guid threadId, Guid teacherId, string audioKey, string audioUrl, int audioDurationSeconds, DateTimeOffset recordedAt)
    {
        var at = TeacherThread.ToMicroseconds(recordedAt);
        return new TeacherVoiceDraft(Guid.NewGuid(), teacherId)
        {
            ThreadId = threadId,
            TeacherId = teacherId,
            AudioKey = audioKey,
            AudioUrl = audioUrl,
            AudioDurationSeconds = audioDurationSeconds,
            Status = TeacherVoiceDraftStatus.Pending,
            Attempts = 0,
            NextAttemptAt = at,
            RecordedAt = at,
        };
    }

    public bool IsDueAt(DateTimeOffset now) => Status == TeacherVoiceDraftStatus.Pending && NextAttemptAt <= now;

    public void CompleteTranscription(string transcript, string model, DateTimeOffset transcribedAt)
    {
        EnsurePending();
        var at = TeacherThread.ToMicroseconds(transcribedAt);
        Transcript = transcript.Trim();
        TranscriptionModel = model;
        Attempts++;
        NextAttemptAt = null;
        TranscribedAt = at;
        Status = TeacherVoiceDraftStatus.Ready;
        UpdationDate = at;
    }

    public void FailAttempt(DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay)
    {
        EnsurePending();
        var at = TeacherThread.ToMicroseconds(failedAt);
        Attempts++;
        if (Attempts >= maxAttempts)
        {
            Status = TeacherVoiceDraftStatus.Failed;
            NextAttemptAt = null;
        }
        else
        {
            NextAttemptAt = at + (retryBaseDelay * Math.Pow(2, Attempts - 1));
        }

        UpdationDate = at;
    }

    public void MarkSent(Guid messageId, DateTimeOffset sentAt)
    {
        if (Status == TeacherVoiceDraftStatus.Sent)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherVoiceDraftAlreadySent);
        }

        if (Status == TeacherVoiceDraftStatus.Pending)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherVoiceDraftNotReady);
        }

        SentMessageId = messageId;
        Status = TeacherVoiceDraftStatus.Sent;
        UpdatedBy = TeacherId;
        UpdationDate = TeacherThread.ToMicroseconds(sentAt);
    }

    private void EnsurePending()
    {
        if (Status != TeacherVoiceDraftStatus.Pending)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherVoiceDraftNotPending);
        }
    }
}
