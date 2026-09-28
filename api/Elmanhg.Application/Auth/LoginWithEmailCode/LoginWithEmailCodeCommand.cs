using Elmanhg.Application.Auth.Shared;
using MediatR;

namespace Elmanhg.Application.Auth.LoginWithEmailCode;

public sealed record LoginWithEmailCodeCommand(Guid VerificationId) : IRequest<AuthResult>;
