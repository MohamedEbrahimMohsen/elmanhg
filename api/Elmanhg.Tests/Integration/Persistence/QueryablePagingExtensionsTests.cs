using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class QueryablePagingExtensionsTests(ApiFactory factory)
{
    // Far in the past so a row left behind cannot move any open thread's deadline in parallel tests.
    private static readonly DateOnly Start = new(2001, 8, 1);
    private static readonly DateOnly End = new(2001, 9, 15);
    private static readonly Guid AdminId = Guid.Parse("7d2f3b84-5c6e-4a91-8b2c-3d4e5f6a7b82");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToPageDataAsync_SecondPage_ReturnsRemainderAndTotals()
    {
        var ids = await SeedAsync(3);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var page = await context.ExamPeriods.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToPageDataAsync(2, 2, CancellationToken);

        page.Items.Should().ContainSingle().Which.Id.Should().Be(ids.MaxBy(x => x.ToString()));
        (page.TotalItems, page.TotalPages).Should().Be((3L, 2L));
    }

    [Fact]
    public async Task ToPageDataAsync_PageBeyondIntOffset_ReturnsEmptyItemsWithTotals()
    {
        var ids = await SeedAsync(1);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var page = await context.ExamPeriods.Where(x => x.Id == ids[0]).OrderBy(x => x.Id).ToPageDataAsync(int.MaxValue, 100, CancellationToken);

        page.Items.Should().BeEmpty();
        (page.TotalItems, page.TotalPages).Should().Be((1L, 1L));
    }

    [Fact]
    public async Task ToPageDataAsync_NoRows_ReturnsZeroTotals()
    {
        var missingId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var page = await context.ExamPeriods.Where(x => x.Id == missingId).OrderBy(x => x.Id).ToPageDataAsync(1, 20, CancellationToken);

        page.Items.Should().BeEmpty();
        (page.TotalItems, page.TotalPages).Should().Be((0L, 0L));
    }

    private async Task<List<Guid>> SeedAsync(int count)
    {
        var periods = Enumerable.Range(0, count).Select(_ => ExamPeriod.Create("Paging extension probe", Start, End, AdminId)).ToList();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ExamPeriods.AddRange(periods);
        await context.SaveChangesAsync(CancellationToken);
        return periods.Select(x => x.Id).ToList();
    }
}
