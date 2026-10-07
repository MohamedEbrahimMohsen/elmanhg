using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class IssuedRefreshTokenRepositoryTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddIfAbsentAsync_SameTokenFromTwoConcurrentRequests_StoresOneFamily()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var tokenHash = RefreshTokenHash.Compute($"sign-in-token-{Guid.NewGuid():N}");
        var now = DateTimeOffset.UtcNow;
        var families = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await Task.WhenAll(families.Select(x => AddIfAbsentAsync(IssuedRefreshToken.Issue(student.Id, x, tokenHash, now, now.AddDays(7)))));

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().IssuedRefreshTokens.AsNoTracking().Where(x => x.TokenHash == tokenHash).ToListAsync(CancellationToken);
        families.Should().Contain(stored.Should().ContainSingle().Which.FamilyId);
    }

    private async Task AddIfAbsentAsync(IssuedRefreshToken token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIssuedRefreshTokenRepository>().AddIfAbsentAsync(token, CancellationToken).ConfigureAwait(false);
    }
}
