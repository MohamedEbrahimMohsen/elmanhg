using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetVoiceDraft;

public sealed record GetVoiceDraftQuery(Guid ThreadId, Guid DraftId) : IRequest<TeacherVoiceDraftResult>;
