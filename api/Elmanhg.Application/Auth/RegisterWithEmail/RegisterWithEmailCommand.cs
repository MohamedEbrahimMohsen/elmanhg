using Elmanhg.Application.Auth.Shared;
using MediatR;

namespace Elmanhg.Application.Auth.RegisterWithEmail;

public sealed record RegisterWithEmailCommand(string DisplayName, string Email, string Password) : IRequest<AuthResult>;
