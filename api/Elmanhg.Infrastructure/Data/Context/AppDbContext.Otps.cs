using Core.OTP.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    // One OTP row per recipient; FindByRecipientAsync and the resend limits depend on it.
    public const string OtpRecipientIndex = "IX_Otps_PhoneNumber";

    private static void ConfigureOtps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Otp>(builder => builder.HasIndex(x => x.Recipient, OtpRecipientIndex).IsUnique().HasFilter("\"IsDeleted\" = false"));
    }
}
