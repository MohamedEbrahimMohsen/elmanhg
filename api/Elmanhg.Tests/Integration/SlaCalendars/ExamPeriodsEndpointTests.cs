using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Configuration;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Configuration.ConfigurationTestData;
using static Elmanhg.Tests.Integration.SlaCalendars.SlaCalendarTestData;

namespace Elmanhg.Tests.Integration.SlaCalendars;

[Collection(RuntimeSettingsCollection.Name)]
public sealed class ExamPeriodsEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await ResetAsync();

    public async ValueTask DisposeAsync() => await ResetAsync();

    [Fact]
    public async Task Post_Valid_CreatesPeriodAndWritesAudit()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PostAsJsonAsync(ExamPeriodsRoute, new { name = " Final exams ", startDate = "2026-06-01", endDate = "2026-07-15" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var id = body.GetProperty("id").GetGuid();
        (body.GetProperty("name").GetString(), body.GetProperty("startDate").GetString(), body.GetProperty("endDate").GetString()).Should().Be(("Final exams", "2026-06-01", "2026-07-15"));
        var stored = await ReadAsync(id);
        (stored!.Name, stored.StartDate, stored.EndDate, stored.IsDeleted).Should().Be(("Final exams", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15), false));
        (await ReadAuditsAsync(factory, "ExamPeriod.Create", id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Post_EndBeforeStart_Returns422DateRangeInvalid()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PostAsJsonAsync(ExamPeriodsRoute, new { name = "Final exams", startDate = "2026-07-15", endDate = "2026-06-01" }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.UnprocessableEntity, "EXAM_PERIOD_DATE_RANGE_INVALID");
    }

    [Fact]
    public async Task Post_AsTeacher_Returns403()
    {
        using var teacher = await TeacherClientAsync(factory);

        using var response = await teacher.PostAsJsonAsync(ExamPeriodsRoute, new { name = "Final exams", startDate = "2026-06-01", endDate = "2026-07-15" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(ExamPeriodsRoute, new { name = "Final exams", startDate = "2026-06-01", endDate = "2026-07-15" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AsAdmin_ListsNewestStartFirst()
    {
        using var admin = await AdminClientAsync(factory);
        await CreateAsync(admin, "First round", "2026-01-10", "2026-01-20");
        await CreateAsync(admin, "Final exams", "2026-06-01", "2026-07-15");

        var body = await admin.GetFromJsonAsync<JsonElement>(ExamPeriodsRoute, CancellationToken);

        body.EnumerateArray().Select(x => x.GetProperty("startDate").GetString()).Should().Equal("2026-06-01", "2026-01-10");
    }

    [Fact]
    public async Task Put_Existing_UpdatesAndWritesAudit()
    {
        using var admin = await AdminClientAsync(factory);
        var id = await CreateAsync(admin, "Final exams", "2026-06-01", "2026-07-15");

        using var response = await admin.PutAsJsonAsync(ExamPeriodRoute(id), new { name = "Second round", startDate = "2026-08-01", endDate = "2026-08-10" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await ReadAsync(id);
        (stored!.Name, stored.StartDate, stored.EndDate).Should().Be(("Second round", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)));
        (await ReadAuditsAsync(factory, "ExamPeriod.Update", id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Put_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync(ExamPeriodRoute(Guid.NewGuid()), new { name = "Second round", startDate = "2026-08-01", endDate = "2026-08-10" }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "EXAM_PERIOD_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_Existing_HidesPeriodAndWritesAudit()
    {
        using var admin = await AdminClientAsync(factory);
        var id = await CreateAsync(admin, "Final exams", "2026-06-01", "2026-07-15");

        using var response = await admin.DeleteAsync(ExamPeriodRoute(id), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await admin.GetFromJsonAsync<JsonElement>(ExamPeriodsRoute, CancellationToken);
        list.EnumerateArray().Should().BeEmpty();
        (await ReadAsync(id))!.IsDeleted.Should().BeTrue();
        (await ReadAuditsAsync(factory, "ExamPeriod.Delete", id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.DeleteAsync(ExamPeriodRoute(Guid.NewGuid()), CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "EXAM_PERIOD_NOT_FOUND");
    }

    [Fact]
    public async Task PutSetting_SevenWeekendDaysWithSkipOn_Returns400()
    {
        await SetOverrideAsync(factory, "slaCalendar.skipWeekends", "true");
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync(SettingRoute("slaCalendar.weekendDays"), new { value = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" } }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.BadRequest, "SLA_CALENDAR_WEEKEND_DAYS_INVALID");
    }

    private async Task ResetAsync()
    {
        await ClearOverridesAsync(factory);
        await ClearExamPeriodsAsync(factory);
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, string name, string startDate, string endDate)
    {
        using var response = await admin.PostAsJsonAsync(ExamPeriodsRoute, new { name, startDate, endDate }, CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("id").GetGuid();
    }

    private async Task<Elmanhg.Domain.SlaCalendars.ExamPeriod?> ReadAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExamPeriods.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, CancellationToken);
    }

    private static async Task ExpectProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be(code);
    }
}
