using Microsoft.AspNetCore.Authorization;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class ThrowingAuthorizationHandler(Exception exception) : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context) => throw exception;
}
