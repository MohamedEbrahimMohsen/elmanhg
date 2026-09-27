using Elmanhg.Application.Auth.Shared;
using MediatR;

namespace Elmanhg.Application.Auth.LoginWithPhone;

public sealed record LoginWithPhoneCommand(Guid VerificationId) : IRequest<AuthResult>;
