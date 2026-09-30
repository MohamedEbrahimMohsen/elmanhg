using Elmanhg.Domain.Analytics;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class UserActivityDayPersistenceTests(ApiFactory factory)
{
    private static readonly DateOnly Day = new(2020, 7, 1);
    private static readonly DateTimeOffset FirstSeenAt = new(2020, 7, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddIfAbsent_SameUserSameDayTwice_KeepsOneRowWithFirstSeenAt()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        await AddIfAbsentAsync(UserActivityDay.Record(student.Id, Day, FirstSeenAt));

        await AddIfAbsentAsync(UserActivityDay.Record(student.Id, Day, FirstSeenAt.AddHours(3)));

        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserActivityDays.AsNoTracking().Where(x => x.UserId == student.Id && x.Day == Day).ToListAsync(CancellationToken);
        rows.Should().ContainSingle().Which.FirstSeenAt.Should().Be(FirstSeenAt);
    }

    private async Task AddIfAbsentAsync(UserActivityDay activity)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserActivityDayRepository>().AddIfAbsentAsync(activity, CancellationToken).ConfigureAwait(false);
    }
}
