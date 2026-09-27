using Elmanhg.Application.Auth.Shared;
using MediatR;

namespace Elmanhg.Application.Auth.LoginWithEmail;

public sealed record LoginWithEmailCommand(string Email, string Password) : IRequest<AuthResult>;
