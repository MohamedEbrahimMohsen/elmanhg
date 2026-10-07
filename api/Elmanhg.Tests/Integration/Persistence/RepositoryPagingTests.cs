using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Infrastructure.EssayGrading;
using Elmanhg.Infrastructure.MathStepGrading;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class RepositoryPagingTests(ApiFactory factory)
{
    // Far in the past so a row left behind cannot move any open thread's deadline in parallel tests.
    private static readonly DateOnly Start = new(2001, 6, 1);
    private static readonly DateOnly End = new(2001, 7, 15);
    private static readonly Guid AdminId = Guid.Parse("5c1e2a73-4b6d-4f80-9a1b-2c3d4e5f6a71");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindPaginatedAsync_PageBeyondIntOffset_ReturnsEmptyPageWithTotals()
    {
        var ids = await SeedAsync(1);
        using var scope = factory.Services.CreateScope();
        var repository = new Repository<ExamPeriod>(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var page = await repository.FindPaginatedAsync(int.MaxValue, 100, CancellationToken, filter: x => x.Id == ids[0]);

        page.Items.Should().BeEmpty();
        (page.TotalItems, page.TotalPages).Should().Be((1L, 1L));
    }

    [Fact]
    public async Task FindPaginatedAsync_SecondPage_ReturnsRemainderAndTotalPages()
    {
        var ids = await SeedAsync(3);
        using var scope = factory.Services.CreateScope();
        var repository = new Repository<ExamPeriod>(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var page = await repository.FindPaginatedAsync(2, 2, CancellationToken, filter: x => ids.Contains(x.Id), orderBy: query => query.OrderBy(x => x.Id));

        page.Items.Should().ContainSingle().Which.Id.Should().Be(ids.MaxBy(x => x.ToString()));
        (page.TotalItems, page.TotalPages).Should().Be((3L, 2L));
    }

    [Fact]
    public async Task EssayGradeGetInReviewPageAsync_PageBeyondIntOffset_ReturnsEmptyPage()
    {
        using var scope = factory.Services.CreateScope();
        var repository = new EssayGradeRepository(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), TimeProvider.System);

        var page = await repository.GetInReviewPageAsync(Guid.NewGuid(), int.MaxValue, 50, CancellationToken);

        page.Items.Should().BeEmpty();
        (page.TotalItems, page.TotalPages).Should().Be((0L, 0L));
    }

    [Fact]
    public async Task MathStepGradeGetInReviewPageAsync_PageBeyondIntOffset_ReturnsEmptyPage()
    {
        using var scope = factory.Services.CreateScope();
        var repository = new MathStepGradeRepository(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), TimeProvider.System);

        var page = await repository.GetInReviewPageAsync(Guid.NewGuid(), int.MaxValue, 50, CancellationToken);

        page.Items.Should().BeEmpty();
        (page.TotalItems, page.TotalPages).Should().Be((0L, 0L));
    }

    private async Task<List<Guid>> SeedAsync(int count)
    {
        var periods = Enumerable.Range(0, count).Select(_ => ExamPeriod.Create("Paging probe", Start, End, AdminId)).ToList();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ExamPeriods.AddRange(periods);
        await context.SaveChangesAsync(CancellationToken);
        return periods.Select(x => x.Id).ToList();
    }
}
