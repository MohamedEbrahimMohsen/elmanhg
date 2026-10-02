using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class ExamPeriodPersistenceTests(ApiFactory factory)
{
    // Far in the past so a row left behind cannot move any open thread's deadline in parallel tests.
    private static readonly DateOnly Start = new(2001, 6, 1);
    private static readonly DateOnly End = new(2001, 7, 15);
    private static readonly Guid AdminId = Guid.Parse("8b2d4f61-0a3c-4e5d-9f7a-1c2b3d4e5f60");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_ExamPeriod_RoundTripsDateOnlyColumns()
    {
        var period = await SeedAsync();

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExamPeriods.AsNoTracking().SingleAsync(x => x.Id == period.Id, CancellationToken);

        (stored.Name, stored.StartDate, stored.EndDate).Should().Be(("Final exams", Start, End));
    }

    [Fact]
    public async Task SaveChanges_StaleVersion_ThrowsExamPeriodModifiedConcurrently()
    {
        var period = await SeedAsync();
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstRow = await first.ExamPeriods.SingleAsync(x => x.Id == period.Id, CancellationToken);
        var secondRow = await second.ExamPeriods.SingleAsync(x => x.Id == period.Id, CancellationToken);
        firstRow.Update("First edit", Start, End, AdminId);
        await first.SaveChangesAsync(CancellationToken);
        secondRow.Update("Second edit", Start, End, AdminId);

        var act = () => second.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamPeriodModifiedConcurrently);
    }

    private async Task<ExamPeriod> SeedAsync()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, AdminId);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ExamPeriods.Add(period);
        await context.SaveChangesAsync(CancellationToken);
        return period;
    }
}
