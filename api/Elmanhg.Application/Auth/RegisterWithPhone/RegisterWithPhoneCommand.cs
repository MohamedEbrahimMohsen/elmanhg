using Elmanhg.Application.Auth.Shared;
using MediatR;

namespace Elmanhg.Application.Auth.RegisterWithPhone;

public sealed record RegisterWithPhoneCommand(Guid VerificationId, string DisplayName) : IRequest<AuthResult>;
