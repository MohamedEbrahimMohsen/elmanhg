using MediatR;

namespace Elmanhg.Application.Users.CheckUserActive;

public sealed record CheckUserActiveQuery(Guid UserId, string SecurityStampFingerprint) : IRequest<bool>;
