using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Identity;

public class IssuedRefreshTokenRepository(AppDbContext context) : Repository<IssuedRefreshToken>(context), IIssuedRefreshTokenRepository
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
