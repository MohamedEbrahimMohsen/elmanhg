using Elmanhg.Application.Auth.Shared;
using MediatR;

namespace Elmanhg.Application.Auth.AcceptInvitation;

public sealed record AcceptInvitationCommand(Guid VerificationId, string Password) : IRequest<AuthResult>;
