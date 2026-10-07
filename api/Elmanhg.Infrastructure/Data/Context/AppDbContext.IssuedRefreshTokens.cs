using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public DbSet<IssuedRefreshToken> IssuedRefreshTokens { get; set; }

    private static void ConfigureIssuedRefreshTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IssuedRefreshToken>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(Sha256HexLength);
            builder.Ignore(x => x.IsRevoked);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.FamilyId);
            builder.HasIndex(x => x.ExpiresAt);
        });
    }
}
