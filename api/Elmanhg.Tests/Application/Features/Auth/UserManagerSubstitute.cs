using Elmanhg.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Auth;

public static class UserManagerSubstitute
{
    public static UserManager<User> Create()
    {
        return Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
