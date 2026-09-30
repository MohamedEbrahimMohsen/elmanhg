using ClosedXML.Excel;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Content;

public sealed class QuestionImportEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/question-imports";
    private const string XlsxMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] McqHeaders = ["stem", "option_a", "option_b", "correct", "difficulty"];

    [Fact]
    public async Task GetTemplate_Admin_ReturnsWorkbookWithInstructionsAndTypeSheets()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/template", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxMediaType);
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync(cancellationToken)));
        workbook.Worksheets.Select(x => x.Name).Should().Equal("Instructions", "Mcq", "Multi", "TrueFalse", "Fill", "Short");
        workbook.Worksheet("Mcq").Row(1).CellsUsed().Select(x => x.GetString()).Should().Contain(["stem", "option_a", "correct"]);
    }

    [Fact]
    public async Task GetTemplate_Teacher_Returns403()
    {
        using var teacher = await TeacherClientAsync();

        using var response = await teacher.GetAsync($"{Route}/template", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostPreview_ValidWorkbook_ReturnsCountsAndSavesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, ValidWorkbook());

        using var response = await admin.PostAsync($"{Route}/preview", form, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        (body.GetProperty("totalRows").GetInt32(), body.GetProperty("validRows").GetInt32()).Should().Be((2, 2));
        body.GetProperty("errors").GetArrayLength().Should().Be(0);
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task PostPreview_InvalidRow_ReturnsRowError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, new QuestionWorkbookBuilder().Sheet("Mcq", McqHeaders, ["2 + 2 = ?", "3", "4", "z", "medium"]).Build());

        using var response = await admin.PostAsync($"{Route}/preview", form, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("errors")[0];
        (error.GetProperty("sheet").GetString(), error.GetProperty("row").GetInt32(), error.GetProperty("code").GetString()).Should().Be(("Mcq", 2, "QUESTION_CORRECT_OPTION_INVALID"));
    }

    [Fact]
    public async Task PostPreview_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();
        using var form = Form(Guid.NewGuid(), ValidWorkbook());

        using var response = await admin.PostAsync($"{Route}/preview", form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task PostPreview_CsvFile_Returns422FileTypeInvalid()
    {
        var lessonId = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, Encoding.UTF8.GetBytes("stem,correct"), fileName: "q.csv");

        using var response = await admin.PostAsync($"{Route}/preview", form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_IMPORT_FILE_TYPE_INVALID");
    }

    [Fact]
    public async Task PostPreview_CorruptFile_Returns400SpreadsheetUnreadable()
    {
        var lessonId = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, [0x50, 0x4B, 0x03, 0x04, .. Encoding.UTF8.GetBytes("not a workbook")]);

        using var response = await admin.PostAsync($"{Route}/preview", form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("SPREADSHEET_UNREADABLE");
    }

    [Fact]
    public async Task PostPreview_NonZipBytes_Returns422FileTypeInvalid()
    {
        var lessonId = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, Encoding.UTF8.GetBytes("not a workbook"));

        using var response = await admin.PostAsync($"{Route}/preview", form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_IMPORT_FILE_TYPE_INVALID");
    }

    [Fact]
    public async Task PostImport_ValidWorkbook_CreatesPendingQuestionsWithBatchAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        var batchId = Guid.NewGuid();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, ValidWorkbook(), batchId);

        using var response = await admin.PostAsync(Route, form, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await ShouldBeResultAsync(response, createdCount: 2, replayed: false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var questions = await context.Questions.Include(x => x.Revisions).AsNoTracking().Where(x => x.LessonId == lessonId).ToListAsync(cancellationToken);
        questions.Should().HaveCount(2).And.OnlyContain(x => x.ValidationStatus == QuestionValidationStatus.Pending && x.Version == 1 && x.ImportBatchId == batchId && x.Revisions.Count == 1);
        (await context.QuestionImportBatches.AsNoTracking().SingleAsync(x => x.Id == batchId, cancellationToken)).QuestionCount.Should().Be(2);
        (await ContentTestData.ReadAuditAsync(factory, "Question.Import", batchId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PostImport_SameBatchSameFileTwice_ReplaysWithoutDuplicates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        var batchId = Guid.NewGuid();
        var content = ValidWorkbook();
        using var admin = await AdminClientAsync();
        using var first = Form(lessonId, content, batchId);
        using var second = Form(lessonId, content, batchId);
        (await admin.PostAsync(Route, first, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await admin.PostAsync(Route, second, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await ShouldBeResultAsync(response, createdCount: 2, replayed: true);
        (await CountQuestionsAsync(lessonId)).Should().Be(2);
    }

    [Fact]
    public async Task PostImport_SameBatchDifferentFile_Returns409BatchConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        var batchId = Guid.NewGuid();
        using var admin = await AdminClientAsync();
        using var first = Form(lessonId, ValidWorkbook(), batchId);
        using var second = Form(lessonId, new QuestionWorkbookBuilder().Sheet("TrueFalse", ["stem", "correct_answer", "difficulty"], ["Speed is a scalar.", "true", "easy"]).Build(), batchId);
        (await admin.PostAsync(Route, first, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await admin.PostAsync(Route, second, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_IMPORT_BATCH_CONFLICT");
        (await CountQuestionsAsync(lessonId)).Should().Be(2);
    }

    [Fact]
    public async Task PostImport_ConcurrentConfirmOfSameBatch_ReplaysInsteadOfFailing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        var batchId = Guid.NewGuid();
        var content = ValidWorkbook();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, content, batchId);
        await using var connection = new NpgsqlConnection(ConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var hold = new NpgsqlCommand("""INSERT INTO "QuestionImportBatches" ("Id", "LessonId", "FileHash", "QuestionCount", "IsDeleted", "CreationDate", "UpdationDate") VALUES (@id, @lessonId, @hash, 2, FALSE, now(), now())""", connection, transaction);
        hold.Parameters.AddWithValue("id", batchId);
        hold.Parameters.AddWithValue("lessonId", lessonId);
        hold.Parameters.AddWithValue("hash", Convert.ToHexStringLower(SHA256.HashData(content)));
        await hold.ExecuteNonQueryAsync(cancellationToken);

        var request = admin.PostAsync(Route, form, cancellationToken);
        await WaitUntilImportIsBlockedAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        using var response = await request;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await ShouldBeResultAsync(response, createdCount: 2, replayed: true);
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    [Fact]
    public async Task PostImport_RowErrors_Returns400HasErrorsAndCreatesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedLessonAsync();
        var batchId = Guid.NewGuid();
        using var admin = await AdminClientAsync();
        using var form = Form(lessonId, new QuestionWorkbookBuilder().Sheet("Mcq", McqHeaders, ["2 + 2 = ?", "3", "4", "b", "medium"], ["", "3", "4", "b", "medium"]).Build(), batchId);

        using var response = await admin.PostAsync(Route, form, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_IMPORT_HAS_ERRORS");
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().QuestionImportBatches.AnyAsync(x => x.Id == batchId, cancellationToken)).Should().BeFalse();
        (await ContentTestData.ReadAuditAsync(factory, "Question.Import", batchId, cancellationToken)).Outcome.Should().Be("Failure");
    }

    [Fact]
    public async Task PostImport_Anonymous_Returns401()
    {
        var lessonId = await SeedLessonAsync();
        using var client = factory.CreateClient();
        using var form = Form(lessonId, ValidWorkbook(), Guid.NewGuid());

        using var response = await client.PostAsync(Route, form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostImport_Teacher_Returns403()
    {
        var lessonId = await SeedLessonAsync();
        using var teacher = await TeacherClientAsync();
        using var form = Form(lessonId, ValidWorkbook(), Guid.NewGuid());

        using var response = await teacher.PostAsync(Route, form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CountQuestionsAsync(lessonId)).Should().Be(0);
    }

    private static byte[] ValidWorkbook()
    {
        return new QuestionWorkbookBuilder()
            .Sheet("Mcq", McqHeaders, ["2 + 2 = ?", "3", "4", "b", "medium"])
            .Sheet("TrueFalse", ["stem", "correct_answer", "difficulty", "objective"], ["Mass is a vector.", "false", "easy", "1"])
            .Build();
    }

    private static MultipartFormDataContent Form(Guid lessonId, byte[] content, Guid? batchId = null, string fileName = "q.xlsx")
    {
        var form = new MultipartFormDataContent { { new StringContent(lessonId.ToString()), "lessonId" }, { new ByteArrayContent(content), "file", fileName } };
        if (batchId is not null)
        {
            form.Add(new StringContent(batchId.Value.ToString()), "batchId");
        }

        return form;
    }

    private string ConnectionString()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;
    }

    private async Task WaitUntilImportIsBlockedAsync(CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var waiting = await context.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%QuestionImportBatches%'""").SingleAsync(cancellationToken).ConfigureAwait(false);
            if (waiting > 0)
            {
                return;
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The import request never waited on the held batch row.");
    }

    private static async Task ShouldBeResultAsync(HttpResponseMessage response, int createdCount, bool replayed)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        (body.GetProperty("createdCount").GetInt32(), body.GetProperty("replayed").GetBoolean()).Should().Be((createdCount, replayed));
    }

    private async Task<int> CountQuestionsAsync(Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Questions.CountAsync(x => x.LessonId == lessonId, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid> SeedLessonAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken).ConfigureAwait(false);
        return await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law"], cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> TeacherClientAsync()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
