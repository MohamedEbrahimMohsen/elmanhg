using Core.EntityFrameworkCore.Auditing;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AppDbContextAuditStampingTests(ApiFactory factory)
{
    // Far in the past so a row left behind cannot move any open thread's deadline in parallel tests.
    private static readonly DateOnly Start = new(2001, 6, 1);
    private static readonly DateOnly End = new(2001, 7, 15);
    private static readonly Guid AdminId = Guid.Parse("3f6a1c2d-7b4e-4a90-8c5d-6e7f8a9b0c12");
    private static readonly Guid OtherAdminId = Guid.Parse("9d0e1f2a-3b4c-4d5e-8f6a-7b8c9d0e1f23");
    private static readonly Guid ThirdAdminId = Guid.Parse("1a2b3c4d-5e6f-4a7b-9c8d-0e1f2a3b4c56");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void AppDbContext_ResolvedFromApp_HasAuditStampingInterceptor()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];

        interceptors.OfType<AuditStampingInterceptor>().Should().ContainSingle();
    }

    [Fact]
    public async Task SaveChanges_AggregateRepeatsUpdatedByUnderAnotherActor_StampsActingUser()
    {
        var period = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        ActAs(scope.ServiceProvider, OtherAdminId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.ExamPeriods.SingleAsync(x => x.Id == period.Id, CancellationToken);
        row.Update("Second round", Start, End, AdminId);

        await context.SaveChangesAsync(CancellationToken);

        (await ReadAsync(period.Id)).UpdatedBy.Should().Be(OtherAdminId);
    }

    [Fact]
    public async Task SaveChanges_NoActingUser_KeepsAggregateUpdatedBy()
    {
        var period = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.ExamPeriods.SingleAsync(x => x.Id == period.Id, CancellationToken);
        row.Update("Second round", Start, End, ThirdAdminId);

        await context.SaveChangesAsync(CancellationToken);

        (await ReadAsync(period.Id)).UpdatedBy.Should().Be(ThirdAdminId);
    }

    private static void ActAs(IServiceProvider services, Guid userId) =>
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")) };

    private async Task<ExamPeriod> SeedAsync()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, AdminId);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ExamPeriods.Add(period);
        await context.SaveChangesAsync(CancellationToken);
        return period;
    }

    private async Task<ExamPeriod> ReadAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExamPeriods.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }
}
