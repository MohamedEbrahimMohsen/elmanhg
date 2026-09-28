using Elmanhg.Application.Sessions.Shared;
using MediatR;

namespace Elmanhg.Application.Sessions.FinishSession;

public sealed record FinishSessionCommand(Guid SessionId) : IRequest<SessionResult>;
