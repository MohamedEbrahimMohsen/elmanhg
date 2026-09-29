using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.TeacherInbox.RecordVoiceDraft;

public sealed record RecordVoiceDraftCommand(Guid ThreadId, IFormFile? Audio, int DurationSeconds) : IRequest<TeacherVoiceDraftResult>;
