using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Core.Identity.Register;

//public record RegisterCommand<TUser>(TUser User, string Password) : IRequest<RegisterResult> where TUser : IdentityUser, new();