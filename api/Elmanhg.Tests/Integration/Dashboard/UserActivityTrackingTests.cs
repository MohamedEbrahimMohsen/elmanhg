using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class UserActivityTrackingTests(ApiFactory factory)
{
    private const string CairoTimeZone = "Africa/Cairo";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_RecordsActivityForCairoToday()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var before = DashboardWindow.LocalDay(TimeProvider.System.GetUtcNow(), CairoTimeZone);

        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        var after = DashboardWindow.LocalDay(TimeProvider.System.GetUtcNow(), CairoTimeZone);
        var rows = await ReadDaysAsync(student.Id);
        rows.Should().ContainSingle().Which.Should().BeOneOf(before, after);
    }

    [Fact]
    public async Task AuthenticatedRequests_RecordOneRowPerDay()
    {
        var (student, client) = await SessionTestData.SignedInFreeStudentAsync(factory);

        using var first = await client.GetAsync("/api/subjects", CancellationToken);
        using var second = await client.GetAsync("/api/subjects", CancellationToken);

        (first.StatusCode, second.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        (await ReadDaysAsync(student.Id)).Should().ContainSingle();
    }

    private async Task<List<DateOnly>> ReadDaysAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.UserActivityDays.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Day).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
