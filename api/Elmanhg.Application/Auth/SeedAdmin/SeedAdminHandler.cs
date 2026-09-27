using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Auth.SeedAdmin;

public sealed class SeedAdminHandler(UserManager<User> userManager, IOptions<AdminSeedOptions> adminSeedOptions) : IRequestHandler<SeedAdminCommand>
{
    public async Task Handle(SeedAdminCommand request, CancellationToken cancellationToken)
    {
        var options = adminSeedOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Email))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(options.Email).ConfigureAwait(false) is not null)
        {
            return;
        }

        var user = User.CreateAdmin(options.DisplayName, options.Email);
        var result = await userManager.CreateAsync(user, options.Password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InternalServerErrorCoreException(ErrorCodes.AdminSeedFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))));
        }
    }
}
