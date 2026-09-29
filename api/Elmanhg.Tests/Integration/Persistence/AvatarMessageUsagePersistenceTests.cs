using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AvatarMessageUsagePersistenceTests(ApiFactory factory)
{
    private const string Cairo = "Africa/Cairo";

    [Fact]
    public async Task CountOnDayAsync_CairoDayBoundary_CountsOnlyThatDaysRowsOfStudent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        var other = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        await SeedUsageAsync(factory, student.Id, 1, new DateTimeOffset(2026, 10, 1, 20, 30, 0, TimeSpan.Zero));
        await SeedUsageAsync(factory, student.Id, 1, new DateTimeOffset(2026, 10, 1, 22, 30, 0, TimeSpan.Zero));
        await SeedUsageAsync(factory, other.Id, 1, new DateTimeOffset(2026, 10, 1, 20, 30, 0, TimeSpan.Zero));

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAvatarMessageUsageRepository>();
        var firstDay = await repository.CountOnDayAsync(student.Id, Cairo, new DateOnly(2026, 10, 1), cancellationToken);
        var secondDay = await repository.CountOnDayAsync(student.Id, Cairo, new DateOnly(2026, 10, 2), cancellationToken);

        (firstDay, secondDay).Should().Be((1, 1));
    }
}
