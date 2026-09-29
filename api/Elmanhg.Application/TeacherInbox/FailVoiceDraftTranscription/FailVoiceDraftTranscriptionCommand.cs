using MediatR;

namespace Elmanhg.Application.TeacherInbox.FailVoiceDraftTranscription;

public sealed record FailVoiceDraftTranscriptionCommand(Guid DraftId) : IRequest;
