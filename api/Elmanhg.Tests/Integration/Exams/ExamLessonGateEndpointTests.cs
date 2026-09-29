using Elmanhg.Application.Shared.Options;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Browse;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class ExamLessonGateEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_UnitExamGateOnLessonUnopened_Returns400ExamLessonsNotOpened()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, mcqCount: 2);
        await using var gated = Gated();
        var (student, client) = await GatedStudentAsync(gated);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{unitId}", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_LESSONS_NOT_OPENED");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.CountAsync(x => x.StudentId == student, CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Post_UnitExamGateOnLessonOpened_StartsExam()
    {
        var (_, unitId, lessonId, _) = await SeedExamUnitAsync(factory, mcqCount: 2);
        await using var gated = Gated();
        var (_, client) = await GatedStudentAsync(gated);
        using var opening = await BrowseTestData.OpenAsync(client, lessonId);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{unitId}", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("kind").GetString().Should().Be("UnitExam");
    }

    [Fact]
    public async Task Get_OverviewGateOn_ReportsUnopenedLessonCount()
    {
        var (_, unitId, lessonId, _) = await SeedExamUnitAsync(factory, mcqCount: 2);
        await using var gated = Gated();
        var (_, client) = await GatedStudentAsync(gated);

        var before = await UnopenedAsync(client, unitId);
        using var opening = await BrowseTestData.OpenAsync(client, lessonId);
        var after = await UnopenedAsync(client, unitId);

        (before, after).Should().Be((1, 0));
    }

    [Fact]
    public async Task Get_OverviewGateOff_ReportsZeroUnopened()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, mcqCount: 2);
        var (_, client) = await SignedInStudentAsync(factory);

        var unopened = await UnopenedAsync(client, unitId);

        unopened.Should().Be(0);
    }

    [Fact]
    public async Task Post_MultiUnitExamGateOnLessonUnopened_Returns400ExamLessonsNotOpened()
    {
        var (subjectId, unitIds, _) = await MultiUnitExamTestData.SeedMultiUnitSubjectAsync(factory, [10, 10]);
        await using var gated = Gated();
        var (_, client) = await GatedStudentAsync(gated);

        using var response = await MultiUnitExamTestData.StartMultiAsync(client, subjectId, unitIds, 20);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_LESSONS_NOT_OPENED");
    }

    private WebApplicationFactory<Program> Gated() => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<ExamsOptions>(options => options.RequireAllLessonsOpened = true)));

    private async Task<(Guid StudentId, HttpClient Client)> GatedStudentAsync(WebApplicationFactory<Program> gated)
    {
        var (student, signedIn) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        using (signedIn)
        {
            var client = gated.CreateClient();
            client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
            return (student.Id, client);
        }
    }

    private static async Task<int> UnopenedAsync(HttpClient client, Guid unitId)
    {
        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{unitId}", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("unopenedLessonCount").GetInt32();
    }
}
