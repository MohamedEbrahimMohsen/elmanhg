using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Identity;

public class IssuedRefreshTokenRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<IssuedRefreshToken>(context, currentUser, timeProvider), IIssuedRefreshTokenRepository
{
    public async Task AddIfAbsentAsync(IssuedRefreshToken token, CancellationToken cancellationToken)
    {
        await _context.Database
            .ExecuteSqlAsync($"""
                INSERT INTO "IssuedRefreshTokens" ("Id", "UserId", "FamilyId", "TokenHash", "IssuedAt", "ExpiresAt", "IsDeleted")
                VALUES ({token.Id}, {token.UserId}, {token.FamilyId}, {token.TokenHash}, {token.IssuedAt}, {token.ExpiresAt}, false)
                ON CONFLICT ("TokenHash") DO NOTHING
                """, cancellationToken)
            .ConfigureAwait(false);
    }
}
