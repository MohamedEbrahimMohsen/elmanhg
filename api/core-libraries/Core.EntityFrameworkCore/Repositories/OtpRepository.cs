using Core.DDD.Entities;
using Core.DDD.Identity;
using Core.EntityFrameworkCore.Context;
using Core.OTP.Entities;
using Core.OTP.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Repositories;

public class OtpRepository<TUser, TRole, TKey, TContext>(TContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null) : Repository<Otp>(context, currentUser, timeProvider), IOtpRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
    public async Task<Otp?> FindByRecipientAsync(string recipient, CancellationToken cancellationToken)
    {
        return await _dbSet.FirstOrDefaultAsync(otp => otp.Recipient == recipient, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Otp?> FindByVerificationId(Guid verificationId, CancellationToken cancellationToken)
    {
        return await _dbSet.FirstOrDefaultAsync(otp => otp.VerificationId == verificationId, cancellationToken).ConfigureAwait(false);
    }
}

