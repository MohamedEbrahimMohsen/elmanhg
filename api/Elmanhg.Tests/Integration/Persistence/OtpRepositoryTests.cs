using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
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

    private async Task SeedAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Otps.Add(new OtpBuilder().ForPhone(phone).IssuedAt(IssuedAt).Build());
        await context.SaveChangesAsync(CancellationToken);
    }
}
