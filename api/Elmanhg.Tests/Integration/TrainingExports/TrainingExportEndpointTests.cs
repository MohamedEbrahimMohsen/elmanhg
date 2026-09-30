using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;
using static Elmanhg.Tests.Integration.TrainingData.TrainingDataTestData;
using static Elmanhg.Tests.Integration.TrainingExports.TrainingExportTestData;

namespace Elmanhg.Tests.Integration.TrainingExports;

public sealed class TrainingExportEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequestExport_Admin_ReturnsPendingAndWritesAuditRow()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        using var response = await PostRequestAsync(client, "Avatar", from, from.AddDays(31));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("source").GetString(), body.GetProperty("fileName").GetString()).Should().Be(("Pending", "Avatar", "elmanhg-avatar-2026-01-01-2026-02-01.jsonl"));
        var exportId = body.GetProperty("id").GetGuid();
        (await ReadAsync(factory, exportId)).CreatedBy.Should().Be(admin.Id);
        var audit = await ReadAuditAsync(factory, "TrainingExport.Request", exportId);
        (audit?.Outcome, audit?.ActorUserId).Should().Be(("Success", (Guid?)admin.Id));
        audit!.Diff.Should().Contain("\"source\"");
    }

    [Fact]
    public async Task RequestExport_InvalidRange_Returns422DateRangeInvalid()
    {
        using var client = await AdminClientAsync();
        var from = DateTimeOffset.UtcNow;

        using var response = await PostRequestAsync(client, "Attempts", from, from.AddDays(-1));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("TRAINING_EXPORT_DATE_RANGE_INVALID");
    }

    [Fact]
    public async Task RequestExport_UnknownSubject_Returns404SubjectNotFound()
    {
        using var client = await AdminClientAsync();
        var from = DateTimeOffset.UtcNow;

        using var response = await PostRequestAsync(client, "Attempts", from, from.AddDays(1), Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task RequestExport_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);
        var from = DateTimeOffset.UtcNow;

        using var response = await PostRequestAsync(client, "Attempts", from, from.AddDays(1));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestExport_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();
        var from = DateTimeOffset.UtcNow;

        using var response = await PostRequestAsync(client, "Attempts", from, from.AddDays(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExports_Admin_ReturnsNewestFirst()
    {
        using var client = await AdminClientAsync();
        var older = await RequestAsync(client, "Attempts");
        var newer = await RequestAsync(client, "EssayGrades");

        using var response = await client.GetAsync($"{ExportsRoute}?pageNumber=1&pageSize=50", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        ids.IndexOf(newer).Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(ids.IndexOf(older));
    }

    [Fact]
    public async Task GetExports_Student_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync(ExportsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FullFlow_AttemptsExport_DownloadsScrubbedJsonl()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        var (student, studentClient) = await SignedInStudentAsync(factory);
        var sessionId = (await StartQuizAsync(studentClient, lessonId)).GetProperty("id").GetGuid();
        using (var answer = await AnswerAsync(studentClient, sessionId, questionIds[0], "b"))
        {
            answer.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var attemptId = (await ReadAttemptsAsync(factory, sessionId)).Single().Id;
        var (subjectId, _) = await ReadLessonScopeAsync(factory, lessonId);
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var exportId = await RequestAsync(client, "Attempts", subjectId);
        await RunAsync(factory, exportId);

        using var response = await client.GetAsync(FileRoute(exportId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-ndjson");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName!.Trim('"').Should().StartWith("elmanhg-attempts-").And.EndWith(".jsonl");
        (response.Headers.CacheControl!.NoStore, response.Headers.CacheControl.Private).Should().Be((true, true));
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        var body = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(CancellationToken));
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var line = JsonDocument.Parse(lines.Should().ContainSingle().Subject).RootElement;
        (line.GetProperty("studentHash").GetString(), line.GetProperty("questionId").GetGuid(), line.GetProperty("sessionKind").GetString()).Should().Be((ExpectedHash(student.Id), questionIds[0], "Quiz"));
        body.Should().NotContain(student.Id.ToString()).And.NotContain(attemptId.ToString()).And.NotContain(sessionId.ToString());
        var export = await ReadAsync(factory, exportId);
        (export.RowCount, export.FileSizeBytes).Should().Be(((long?)1, (long?)Encoding.UTF8.GetByteCount(body)));
        (await ReadAuditAsync(factory, "TrainingExport.Download", exportId))?.Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task DownloadFile_PendingExport_Returns409NotReady()
    {
        using var client = await AdminClientAsync();
        var exportId = await RequestAsync(client, "Attempts");

        using var response = await client.GetAsync(FileRoute(exportId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TRAINING_EXPORT_NOT_READY");
        (await ReadAuditAsync(factory, "TrainingExport.Download", exportId))?.Outcome.Should().Be("Failure");
    }

    [Fact]
    public async Task DownloadFile_UnknownExport_Returns404()
    {
        using var client = await AdminClientAsync();

        using var response = await client.GetAsync(FileRoute(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TRAINING_EXPORT_NOT_FOUND");
    }

    [Fact]
    public async Task DownloadFile_Teacher_Returns403()
    {
        using var admin = await AdminClientAsync();
        var exportId = await RequestAsync(admin, "Attempts");
        await RunAsync(factory, exportId);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(FileRoute(exportId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DownloadFile_Anonymous_Returns401()
    {
        using var admin = await AdminClientAsync();
        var exportId = await RequestAsync(admin, "Attempts");
        await RunAsync(factory, exportId);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(FileRoute(exportId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Media_TrainingExportFileUnderMediaPath_Returns404()
    {
        var folder = Path.Combine(factory.MediaRoot, "training-exports");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "x.jsonl"), "{}\n", CancellationToken);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/media/training-exports/x.jsonl", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
    }
}
