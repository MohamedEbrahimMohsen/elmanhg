using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using static Elmanhg.Tests.Integration.Exams.ExamAttemptsTestData;
using static Elmanhg.Tests.Integration.Progress.ProgressTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Progress;

public sealed class SessionHistoryBestScoreEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Day1 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_ExamSittings_FlagsBestPerScope()
    {
        var (subjectId, unitA, unitB) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (student, client) = await SignedInStudentAsync(factory);
        var a60 = await InsertUnitSittingAsync(factory, student.Id, unitA, 60m, startedAt: Day1);
        var a80 = await InsertUnitSittingAsync(factory, student.Id, unitA, 80m, startedAt: Day1.AddDays(1));
        var b50 = await InsertUnitSittingAsync(factory, student.Id, unitB, 50m, startedAt: Day1.AddDays(2));
        var multi70 = await InsertMultiSittingAsync(factory, student.Id, subjectId, [unitA, unitB], 20, 70m, Day1.AddDays(3));
        var testMode = await InsertUnitSittingAsync(factory, student.Id, unitA, 99m, isTestMode: true, startedAt: Day1.AddDays(4));

        var body = await GetJsonAsync(client, "sessions?kind=Exam");

        var flags = body.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetGuid(), x => x.GetProperty("isBestScore").GetBoolean());
        flags.Should().BeEquivalentTo(new Dictionary<Guid, bool> { [a60] = false, [a80] = true, [b50] = true, [multi70] = true });
        flags.Should().NotContainKey(testMode);
    }
}
