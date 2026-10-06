using Core.Auditing.Entities;
using Core.Storage;
using Elmanhg.Application.TrainingExports.ExpireTrainingExport;
using Elmanhg.Application.TrainingExports.RunTrainingExport;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.TrainingExports;

public static class TrainingExportTestData
{
    public const string ExportsRoute = "/api/training-exports";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string FileRoute(Guid exportId) => $"{ExportsRoute}/{exportId}/file";

    public static Task<HttpResponseMessage> PostRequestAsync(HttpClient client, string source, DateTimeOffset from, DateTimeOffset to, Guid? subjectId = null)
    {
        return client.PostAsJsonAsync(ExportsRoute, new { source, from, to, subjectId }, CancellationToken);
    }

    public static async Task<Guid> RequestAsync(HttpClient client, string source, Guid? subjectId = null)
    {
        var now = DateTimeOffset.UtcNow;
        using var response = await PostRequestAsync(client, source, now.AddDays(-1), now.AddDays(1), subjectId).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("id").GetGuid();
    }

    public static async Task RunAsync(ApiFactory factory, Guid exportId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunTrainingExportCommand(exportId), CancellationToken).ConfigureAwait(false);
    }

    public static async Task ExpireAsync(ApiFactory factory, Guid exportId, DateTimeOffset now)
    {
        using var scope = factory.Services.CreateScope();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        var handler = new ExpireTrainingExportHandler(scope.ServiceProvider.GetRequiredService<ITrainingExportRepository>(), scope.ServiceProvider.GetRequiredService<IFileStorage>(), timeProvider);
        await handler.Handle(new ExpireTrainingExportCommand(exportId), CancellationToken).ConfigureAwait(false);
    }

    public static async Task<TrainingExport> ReadAsync(ApiFactory factory, Guid exportId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TrainingExports.AsNoTracking().SingleAsync(x => x.Id == exportId, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<AuditLog?> ReadAuditAsync(ApiFactory factory, string action, Guid exportId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Action == action && x.ResourceId == exportId, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("code").GetString();
}
