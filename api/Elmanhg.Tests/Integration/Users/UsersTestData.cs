using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Users;

public static class UsersTestData
{
    public const string UsersRoute = "/api/users";
    public const string InvitationsRoute = "/api/users/invitations";

    public static async Task<Guid> InviteAsync(HttpClient admin, string role, string email, CancellationToken cancellationToken)
    {
        using var response = await admin.PostAsJsonAsync(InvitationsRoute, new { role, displayName = "Invited", email }, cancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false)).GetProperty("userId").GetGuid();
    }

    public static async Task<User> ReadUserAsync(ApiFactory factory, Guid userId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == userId, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return (await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false)).GetProperty("code").GetString();
    }
}
