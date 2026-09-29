using MediatR;

namespace Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;

public sealed record TranscribeVoiceDraftCommand(Guid DraftId) : IRequest;
