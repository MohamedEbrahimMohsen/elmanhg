using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Mastery.MasteryTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Mastery;

public sealed class MasteryOverviewEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync($"{MasteryTestData.Route}/overview", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync($"{MasteryTestData.Route}/overview", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Admin_Returns403()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync($"{MasteryTestData.Route}/overview", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NewStudent_ReturnsNothingMasteredAndNoStreak()
    {
        var (subjectId, _, _, _) = await SeedAsync();
        var (_, client) = await SignedInStudentAsync(factory);

        var overview = await GetOverviewAsync(client);

        AssertHeadline(overview, mastered: 0, seen: 0);
        overview.GetProperty("streakDays").GetInt32().Should().Be(0);
        AssertCard(SubjectCard(overview, subjectId), servable: 2, mastered: 0, percent: 0);
    }

    [Fact]
    public async Task Get_AfterTwoCorrectAttempts_CountsQuestionMasteredSeenAndStreak()
    {
        var (subjectId, lessonId, q1, q2) = await SeedAsync();
        var (student, client) = await SignedInStudentAsync(factory);
        await MasterFirstSeeSecondAsync(client, lessonId, q1, q2);

        var overview = await GetOverviewAsync(client);

        AssertHeadline(overview, mastered: 1, seen: 2);
        overview.GetProperty("streakDays").GetInt32().Should().Be(1);
        AssertCard(SubjectCard(overview, subjectId), servable: 2, mastered: 1, percent: 50);
        var rows = await ReadMasteriesAsync(factory, student.Id);
        (rows.Single(x => x.QuestionId == q1).IsMastered, rows.Single(x => x.QuestionId == q2).IsMastered).Should().Be((true, false));
    }

    [Fact]
    public async Task Get_WrongAttemptAfterMastery_LosesMastery()
    {
        var (_, lessonId, q1, q2) = await SeedAsync();
        var (student, client) = await SignedInStudentAsync(factory);
        await MasterFirstSeeSecondAsync(client, lessonId, q1, q2);
        await PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "a" });

        var overview = await GetOverviewAsync(client);

        AssertHeadline(overview, mastered: 0, seen: 2);
        (await ReadMasteriesAsync(factory, student.Id)).Single(x => x.QuestionId == q1).IsMastered.Should().BeFalse();
    }

    [Fact]
    public async Task Get_MasteredQuestionRetired_DropsItFromMasteredAndServable()
    {
        var (subjectId, lessonId, q1, q2) = await SeedAsync();
        var (student, client) = await SignedInStudentAsync(factory);
        await MasterFirstSeeSecondAsync(client, lessonId, q1, q2);
        await RetireAsync(factory, q1);

        var overview = await GetOverviewAsync(client);

        AssertHeadline(overview, mastered: 0, seen: 1);
        AssertCard(SubjectCard(overview, subjectId), servable: 1, mastered: 0, percent: 0);
        (await ReadMasteriesAsync(factory, student.Id)).Single(x => x.QuestionId == q1).IsMastered.Should().BeTrue();
    }

    [Fact]
    public async Task Get_LessonUnpublished_DropsItsQuestionsFromHeadlineAndSubject()
    {
        var (subjectId, lessonId, q1, q2) = await SeedAsync();
        var (_, client) = await SignedInStudentAsync(factory);
        await MasterFirstSeeSecondAsync(client, lessonId, q1, q2);
        await UnpublishAsync(factory, lessonId);

        var overview = await GetOverviewAsync(client);

        AssertHeadline(overview, mastered: 0, seen: 0);
        AssertCard(SubjectCard(overview, subjectId), servable: 0, mastered: 0, percent: 0);
    }

    [Fact]
    public async Task Get_AfterChoosingSubject_ListsItFirstAsInterested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken);
        var second = await ContentTestData.SeedSubjectAsync(factory, "Chemistry", 2, cancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        using var saved = await client.PutAsJsonAsync("/api/students/me/subject-interests", new { subjectIds = new[] { second } }, cancellationToken);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);

        var overview = await GetOverviewAsync(client);

        var firstCard = overview.GetProperty("subjects")[0];
        firstCard.GetProperty("subjectId").GetGuid().Should().Be(second);
        firstCard.GetProperty("isInterested").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Post_AdminTestModeAnswer_WritesNoQuestionMastery()
    {
        var (_, lessonId, q1, _) = await SeedAsync();
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);

        await PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b" });

        (await ReadMasteriesAsync(factory, admin.Id)).Should().BeEmpty();
    }

    private async Task<(Guid SubjectId, Guid LessonId, Guid Q1, Guid Q2)> SeedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, cancellationToken).ConfigureAwait(false);
        var q1 = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken).ConfigureAwait(false);
        var q2 = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId, q1, q2);
    }

    private static async Task MasterFirstSeeSecondAsync(HttpClient client, Guid lessonId, Guid q1, Guid q2)
    {
        await PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b", [q2] = "a" }).ConfigureAwait(false);
        await PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b" }).ConfigureAwait(false);
    }

    private static void AssertHeadline(JsonElement overview, int mastered, int seen)
    {
        var headline = overview.GetProperty("headline");
        var total = headline.GetProperty("servableTotal").GetInt32();
        (headline.GetProperty("masteredCount").GetInt32(), headline.GetProperty("seenCount").GetInt32()).Should().Be((mastered, seen));
        headline.GetProperty("remainingCount").GetInt32().Should().Be(total - mastered);
    }

    private static void AssertCard(JsonElement card, int servable, int mastered, int percent)
    {
        (card.GetProperty("servableCount").GetInt32(), card.GetProperty("masteredCount").GetInt32(), card.GetProperty("masteryPercent").GetInt32()).Should().Be((servable, mastered, percent));
    }
}
