using Core.Storage;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Elmanhg.Tests.Integration.TrainingExports.TrainingExportTestData;

namespace Elmanhg.Tests.Integration.TrainingExports;

public sealed class TrainingExportRetentionEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Expire_AfterRetention_DeletesStoredFileAndDownloadReturns409Expired()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var exportId = await RequestAsync(client, "EssayGrades");
        await RunAsync(factory, exportId);
        var completed = await ReadAsync(factory, exportId);
        var path = Path.Combine(factory.MediaRoot, completed.FileKey!);
        File.Exists(path).Should().BeTrue();
        (completed.ExpiresAt - completed.CompletedAt).Should().Be(TimeSpan.FromDays(7));

        await ExpireAsync(factory, exportId, completed.ExpiresAt!.Value);

        File.Exists(path).Should().BeFalse();
        var expired = await ReadAsync(factory, exportId);
        (expired.Status, expired.FileKey).Should().Be((TrainingExportStatus.Expired, (string?)null));
        using var response = await client.GetAsync(FileRoute(exportId), CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TRAINING_EXPORT_EXPIRED");
    }

    [Fact]
    public async Task Expire_BeforeRetention_KeepsFileAndCompletedStatus()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var exportId = await RequestAsync(client, "EssayGrades");
        await RunAsync(factory, exportId);
        var completed = await ReadAsync(factory, exportId);

        await ExpireAsync(factory, exportId, completed.ExpiresAt!.Value.AddMinutes(-1));

        File.Exists(Path.Combine(factory.MediaRoot, completed.FileKey!)).Should().BeTrue();
        (await ReadAsync(factory, exportId)).Status.Should().Be(TrainingExportStatus.Completed);
    }

    [Fact]
    public async Task Expire_FailedExportWithLeftoverFile_IsListedAndDeletesFile()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var exportId = await RequestAsync(client, "EssayGrades");
        var key = $"training-exports/{exportId:N}-leftover.jsonl";
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITrainingExportRepository>();
            var export = await repository.FirstOrDefaultAsync(x => x.Id == exportId, CancellationToken);
            export!.BeginRun(key, now, TimeSpan.FromMinutes(30));
            export.FailAttempt("IOException", now, 1, TimeSpan.FromSeconds(60));
            await repository.SaveChangesAsync(CancellationToken);
            using var content = new MemoryStream("{}\n"u8.ToArray());
            await scope.ServiceProvider.GetRequiredService<IFileStorage>().SaveAsync(content, key, CancellationToken);
            (await repository.GetExpiredIdsAsync(now, int.MaxValue, [], CancellationToken)).Should().Contain(exportId);
        }

        await ExpireAsync(factory, exportId, now);

        File.Exists(Path.Combine(factory.MediaRoot, key)).Should().BeFalse();
        var failed = await ReadAsync(factory, exportId);
        (failed.Status, failed.FileKey).Should().Be((TrainingExportStatus.Failed, (string?)null));
    }
}
