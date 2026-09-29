using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public static class TeacherVoiceDraftResultGenerator
{
    public static TeacherVoiceDraftResult Generate(TeacherVoiceDraft draft) => new(draft.Id, draft.ThreadId, draft.Status, draft.Transcript, draft.AudioDurationSeconds, draft.RecordedAt, draft.TranscribedAt);
}
