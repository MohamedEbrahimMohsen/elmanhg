using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Application.Features.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Elmanhg.Tests.Core.Identity;

internal static class SignInManagerSubstitute
{
    public static SignInManager<User> Create() => Substitute.For<SignInManager<User>>(UserManagerSubstitute.Create(), Substitute.For<IHttpContextAccessor>(), Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
}
