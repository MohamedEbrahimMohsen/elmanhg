using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetDueVoiceDraftIds;

public sealed record GetDueVoiceDraftIdsQuery : IRequest<List<Guid>>;
