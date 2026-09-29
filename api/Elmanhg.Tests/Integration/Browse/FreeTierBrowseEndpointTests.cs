using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Browse;

public sealed class FreeTierBrowseEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetUnit_FreeStudent_LocksAllButFirstLesson()
    {
        var (unitId, _) = await SeedUnitAsync();
        var (_, client) = await SignedInFreeStudentAsync(factory);

        var body = await BrowseTestData.GetAsync(client, $"units/{unitId}");

        body.GetProperty("lessons").EnumerateArray().Select(x => x.GetProperty("isLocked").GetBoolean()).Should().Equal(false, true);
    }

    [Fact]
    public async Task GetLesson_FreeStudentLockedLesson_ReturnsNoContent()
    {
        var (_, lockedId) = await SeedUnitAsync();
        var (_, client) = await SignedInFreeStudentAsync(factory);

        var body = await BrowseTestData.GetAsync(client, $"lessons/{lockedId}");

        (body.GetProperty("isLocked").GetBoolean(), body.GetProperty("explanation").GetString(), body.GetProperty("name").GetString()).Should().Be((true, string.Empty, "Energy"));
        body.GetProperty("objectives").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetUnit_SubscribedStudent_LocksNoLesson()
    {
        var (unitId, _) = await SeedUnitAsync();
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await BrowseTestData.GetAsync(client, $"units/{unitId}");

        body.GetProperty("lessons").EnumerateArray().Select(x => x.GetProperty("isLocked").GetBoolean()).Should().Equal(false, false);
    }

    private async Task<(Guid UnitId, Guid SecondLessonId)> SeedUnitAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Forces", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        var second = await ContentTestData.SeedLessonAsync(factory, unitId, "Energy", 2, ["Define energy"], CancellationToken).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.SingleAsync(x => x.Id == second, CancellationToken).ConfigureAwait(false);
        lesson.Publish(Guid.NewGuid());
        lesson.ClearDomainEvents();
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (unitId, second);
    }
}
