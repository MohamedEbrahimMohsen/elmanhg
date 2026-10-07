using Core.EntityFrameworkCore.Repositories;
using Core.OTP.Entities;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class OtpRepositoryTests(ApiFactory factory)
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindByRecipientAsync_SavedOtp_ReturnsItWithWindowStart()
    {
        var phone = AuthTestClient.NewPhoneNumber();
        await SeedAsync(phone);
        using var scope = factory.Services.CreateScope();
        var repository = new OtpRepository<User, Role, Guid, AppDbContext>(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var otp = await repository.FindByRecipientAsync(phone, CancellationToken);

        otp.Should().NotBeNull();
        otp!.ReissueWindowStartedAt.Should().Be(IssuedAt);
    }

    [Fact]
    public async Task FindByRecipientAsync_UnknownRecipient_ReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var repository = new OtpRepository<User, Role, Guid, AppDbContext>(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var otp = await repository.FindByRecipientAsync(AuthTestClient.NewPhoneNumber(), CancellationToken);

        otp.Should().BeNull();
    }

    [Fact]
    public async Task AddIfAbsentAsync_NewRecipient_InsertsRowAndReturnsTrue()
    {
        var phone = AuthTestClient.NewPhoneNumber();
        using var scope = factory.Services.CreateScope();
        var repository = new OtpRepository<User, Role, Guid, AppDbContext>(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var added = await repository.AddIfAbsentAsync(new OtpBuilder().ForPhone(phone).IssuedAt(IssuedAt).Build(), CancellationToken);

        added.Should().BeTrue();
        (await CountAsync(phone)).Should().Be(1);
    }

    [Fact]
    public async Task AddIfAbsentAsync_RecipientAlreadyStored_ReturnsFalseAndDetachesTheNewRow()
    {
        var phone = AuthTestClient.NewPhoneNumber();
        await SeedAsync(phone);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new OtpRepository<User, Role, Guid, AppDbContext>(context);
        var newOtp = new OtpBuilder().ForPhone(phone).IssuedAt(IssuedAt).Build();

        var added = await repository.AddIfAbsentAsync(newOtp, CancellationToken);

        added.Should().BeFalse();
        context.Entry(newOtp).State.Should().Be(EntityState.Detached);
        (await CountAsync(phone)).Should().Be(1);
    }

    [Fact]
    public async Task AddIfAbsentAsync_OtherInsertFailure_Rethrows()
    {
        var stored = await SeedAsync(AuthTestClient.NewPhoneNumber());
        using var scope = factory.Services.CreateScope();
        var repository = new OtpRepository<User, Role, Guid, AppDbContext>(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        var newOtp = new OtpBuilder().ForPhone(AuthTestClient.NewPhoneNumber()).IssuedAt(IssuedAt).Build();
        newOtp.Id = stored.Id;

        var act = () => repository.AddIfAbsentAsync(newOtp, CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private async Task<Otp> SeedAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otp = new OtpBuilder().ForPhone(phone).IssuedAt(IssuedAt).Build();
        context.Otps.Add(otp);
        await context.SaveChangesAsync(CancellationToken);
        return otp;
    }

    private async Task<int> CountAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Otps.AsNoTracking().CountAsync(x => x.Recipient == phone, CancellationToken);
    }
}
