using Core.DDD.Repositories;
using Core.OTP.Entities;

namespace Core.OTP.Repositories;

public interface IOtpRepository : IRepository<Otp>
{
    Task<Otp?> FindByRecipientAsync(string recipient, CancellationToken cancellationToken);
    Task<Otp?> FindByVerificationId(Guid verificationId, CancellationToken cancellationToken);
    Task<bool> AddIfAbsentAsync(Otp otp, CancellationToken cancellationToken);
}
