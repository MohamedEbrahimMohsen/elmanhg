using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public sealed record TeacherVoiceDraftResult(Guid Id, Guid ThreadId, TeacherVoiceDraftStatus Status, string? Transcript, int AudioDurationSeconds, DateTimeOffset RecordedAt, DateTimeOffset? TranscribedAt);
