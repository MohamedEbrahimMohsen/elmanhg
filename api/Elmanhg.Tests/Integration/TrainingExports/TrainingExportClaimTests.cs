using Core.Errors;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.TrainingExports.TrainingExportTestData;

namespace Elmanhg.Tests.Integration.TrainingExports;

public sealed class TrainingExportClaimTests(ApiFactory factory)
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BeginRun_TwoRunsLoadedTogether_OnlyTheFirstClaimSaves()
    {
        var exportId = await RequestAsync();
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<ITrainingExportRepository>();
        var secondRepository = second.ServiceProvider.GetRequiredService<ITrainingExportRepository>();
        var firstExport = await firstRepository.FirstOrDefaultAsync(x => x.Id == exportId, CancellationToken);
        var secondExport = await secondRepository.FirstOrDefaultAsync(x => x.Id == exportId, CancellationToken);
        var now = DateTimeOffset.UtcNow;
        firstExport!.BeginRun($"training-exports/{exportId:N}-first.jsonl", now, Lease);
        secondExport!.BeginRun($"training-exports/{exportId:N}-second.jsonl", now, Lease);
        await firstRepository.SaveChangesAsync(CancellationToken);

        var act = () => secondRepository.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be("TRAINING_EXPORT_MODIFIED_CONCURRENTLY");
        var stored = await ReadAsync(factory, exportId);
        (stored.FileKey, stored.NextAttemptAt > now).Should().Be(($"training-exports/{exportId:N}-first.jsonl", true));
    }

    [Fact]
    public async Task Run_TwoConcurrentRuns_WriteExactlyOneFile()
    {
        var exportId = await RequestAsync();

        var failures = await Task.WhenAll(Record.ExceptionAsync(() => RunAsync(factory, exportId)).AsTask(), Record.ExceptionAsync(() => RunAsync(factory, exportId)).AsTask());

        var export = await ReadAsync(factory, exportId);
        export.Status.Should().Be(TrainingExportStatus.Completed);
        failures.Where(x => x is not null and not ConflictCoreException { ErrorCode: "TRAINING_EXPORT_MODIFIED_CONCURRENTLY" }).Should().BeEmpty();
        Directory.GetFiles(Path.Combine(factory.MediaRoot, "training-exports"), $"{exportId:N}-*").Select(Path.GetFileName).Should().Equal(Path.GetFileName(export.FileKey));
    }

    private async Task<Guid> RequestAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        return await TrainingExportTestData.RequestAsync(client, "EssayGrades");
    }
}
