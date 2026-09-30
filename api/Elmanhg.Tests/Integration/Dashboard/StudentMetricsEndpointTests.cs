using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class StudentMetricsEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SeededActivityDay_CountsStudentButNotTeacher()
    {
        var day = new DateOnly(2021, 2, 3);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        await SeedActivityAsync(factory, student.Id, day);
        await SeedActivityAsync(factory, teacher.Id, day);
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, "students?from=2021-02-03&to=2021-02-03");

        var daily = body.GetProperty("dailyActive").EnumerateArray().Should().ContainSingle().Subject;
        daily.GetProperty("date").GetString().Should().Be("2021-02-03");
        daily.GetProperty("value").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task Get_StudentCreatedOnSeededDay_CountsInNewInRange()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        await SetCreationDateAsync(factory, student.Id, new DateTimeOffset(2021, 3, 10, 10, 0, 0, TimeSpan.Zero));
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, "students?from=2021-03-10&to=2021-03-10");

        body.GetProperty("newInRange").GetInt32().Should().Be(1);
        body.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }
}
