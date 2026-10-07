using Core.Errors;
using Core.OTP.Entities;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class OtpConcurrencyPersistenceTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_StaleOtpVerify_ThrowsOtpModifiedConcurrently()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = await SeedAsync(new OtpBuilder().ForPhone(AuthTestClient.NewPhoneNumber()).IssuedAt(now).Build());
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstOtp = await first.Otps.SingleAsync(x => x.Id == otp.Id, CancellationToken);
        var secondOtp = await second.Otps.SingleAsync(x => x.Id == otp.Id, CancellationToken);
        firstOtp.Verify("wrong-hash", now);
        await first.SaveChangesAsync(CancellationToken);
        secondOtp.Verify("wrong-hash", now);

        var act = () => second.SaveChangesAsync(CancellationToken);

        var conflict = (await act.Should().ThrowAsync<ConflictCoreException>()).Which;
        (conflict.ErrorCode, conflict.StatusCode).Should().Be((OtpErrorCodes.OtpModifiedConcurrently, 409));
        (await ReadAsync(otp.Id)).VerificationAttempts.Should().Be(1);
    }

    [Fact]
    public async Task SaveChanges_StaleOtpConsume_ThrowsOtpModifiedConcurrently()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = await SeedAsync(new OtpBuilder().ForPhone(AuthTestClient.NewPhoneNumber()).Verified().IssuedAt(now).Build());
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstOtp = await first.Otps.SingleAsync(x => x.Id == otp.Id, CancellationToken);
        var secondOtp = await second.Otps.SingleAsync(x => x.Id == otp.Id, CancellationToken);
        firstOtp.MarkUsed(now);
        await first.SaveChangesAsync(CancellationToken);
        secondOtp.MarkUsed(now);

        var act = () => second.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(OtpErrorCodes.OtpModifiedConcurrently);
    }

    private async Task<Otp> SeedAsync(Otp otp)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Otps.Add(otp);
        await context.SaveChangesAsync(CancellationToken);
        return otp;
    }

    private async Task<Otp> ReadAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Otps.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }
}
