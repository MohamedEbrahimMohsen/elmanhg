using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TrainingExports;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class RetrySchedulePersistenceTests(ApiFactory factory)
{
    private const string ErrorCode = "PROBE_FAILED";
    private static readonly DateTimeOffset FailedAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).AddTicks(7);
    private static readonly DateTimeOffset FailedAtMicroseconds = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(60);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_FailedTrainingExportAttempt_RoundTripsRetryColumns()
    {
        var exportId = await RequestAsync();

        await FailAttemptAsync(exportId);

        var stored = await TrainingExportTestData.ReadAsync(factory, exportId);
        (stored.Attempts, stored.LastErrorCode, stored.NextAttemptAt).Should().Be((1, (string?)ErrorCode, (DateTimeOffset?)FailedAtMicroseconds.Add(RetryBaseDelay)));
    }

    [Fact]
    public async Task GetDueIdsAsync_FailedExportPastItsBackoff_IsListedOnlyAfterTheDelay()
    {
        var exportId = await RequestAsync();
        await FailAttemptAsync(exportId);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrainingExportRepository>();

        var beforeBackoff = await repository.GetDueIdsAsync(FailedAt.AddSeconds(59), 1000, CancellationToken);
        var afterBackoff = await repository.GetDueIdsAsync(FailedAt.AddSeconds(61), 1000, CancellationToken);

        beforeBackoff.Should().NotContain(exportId);
        afterBackoff.Should().Contain(exportId);
    }

    private async Task FailAttemptAsync(Guid exportId)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrainingExportRepository>();
        var export = await repository.FirstOrDefaultAsync(x => x.Id == exportId, CancellationToken);
        export!.FailAttempt(ErrorCode, FailedAt, 3, RetryBaseDelay);
        await repository.SaveChangesAsync(CancellationToken);
    }

    private async Task<Guid> RequestAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        return await TrainingExportTestData.RequestAsync(client, "EssayGrades");
    }
}
