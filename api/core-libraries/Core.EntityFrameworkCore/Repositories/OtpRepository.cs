using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using Core.OTP.Entities;
using Core.OTP.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Repositories;

public class OtpRepository<TUser, TRole, TKey, TContext>(TContext context) : Repository<Otp>(context), IOtpRepository
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

    // Saves at once: a concurrent first send for the same recipient loses on the unique recipient index and must re-read the winner's row.
    public async Task<bool> AddIfAbsentAsync(Otp otp, CancellationToken cancellationToken)
    {
        await _dbSet.AddAsync(otp, cancellationToken).ConfigureAwait(false);
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            _context.Entry(otp).State = EntityState.Detached;
            if (!await _dbSet.AsNoTracking().AnyAsync(x => x.Recipient == otp.Recipient && x.Id != otp.Id, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }

            return false;
        }
    }
}

