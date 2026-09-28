using Elmanhg.Application.Sessions.Shared;
using MediatR;

namespace Elmanhg.Application.Sessions.GetSession;

public sealed record GetSessionQuery(Guid SessionId) : IRequest<SessionResult>;
