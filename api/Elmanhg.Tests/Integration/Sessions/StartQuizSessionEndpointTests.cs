using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class StartQuizSessionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_PublishedLessonWithServableQuestions_Returns200AndPersistsItems()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 3);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await StartQuizAsync(client, lessonId, 5);

        body.GetProperty("items").GetArrayLength().Should().Be(3);
        body.GetProperty("currentPosition").GetInt32().Should().Be(1);
        body.GetProperty("kind").GetString().Should().Be("Quiz");
        var session = await ReadSessionAsync(factory, body.GetProperty("id").GetGuid());
        session.Items.Select(x => x.QuestionId).Should().BeEquivalentTo(questionIds);
        session.Items.Should().AllSatisfy(x => x.QuestionVersion.Should().Be(1));
        session.IsTestMode.Should().BeFalse();
    }

    [Fact]
    public async Task Post_MixedQuestions_ServesOnlyServable()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, TestContext.Current.CancellationToken);
        await QuestionTestData.SeedRetiredQuestionAsync(factory, lessonId, TestContext.Current.CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await StartQuizAsync(client, lessonId, 5);

        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).Should().Equal(questionIds);
    }

    [Fact]
    public async Task Post_QuestionCountBelowAvailable_ServesThatMany()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 6);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await StartQuizAsync(client, lessonId, 5);

        var served = body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();
        served.Should().HaveCount(5).And.OnlyHaveUniqueItems().And.BeSubsetOf(questionIds);
    }

    [Fact]
    public async Task Post_SecondStartSameLesson_ResumesSameSession()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 2);
        var (student, client) = await SignedInStudentAsync(factory);
        var first = await StartQuizAsync(client, lessonId);

        var second = await StartQuizAsync(client, lessonId, 5);

        second.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        using var scope = factory.Services.CreateScope();
        var open = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.CountAsync(x => x.StudentId == student.Id && x.ScopeKey == $"lesson:{lessonId:D}" && x.SubmittedAt == null, TestContext.Current.CancellationToken);
        open.Should().Be(1);
    }

    [Fact]
    public async Task Post_NoServableQuestions_Returns400SessionNoServableQuestions()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 0);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NO_SERVABLE_QUESTIONS");
    }

    [Fact]
    public async Task Post_DraftLesson_Returns404LessonNotFound()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 1, LessonState.Draft, TestContext.Current.CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Post_QuestionCountOutOfRange_Returns422()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 1);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId, questionCount = 21 }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("SESSION_QUESTION_COUNT_INVALID");
    }

    [Fact]
    public async Task Post_Admin_StartsTestModeSession()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 1);
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);

        var body = await StartQuizAsync(client, lessonId);

        body.GetProperty("isTestMode").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 1);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 1);
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AfterFinishedQuiz_ServesUnseenThenLastWrongOldestFirst()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 6);
        var (_, client) = await SignedInStudentAsync(factory);
        var first = await StartQuizAsync(client, lessonId, 5);
        var firstId = first.GetProperty("id").GetGuid();
        var served = ServedQuestionIds(first);
        for (var index = 0; index < served.Count; index++)
        {
            using var answer = await AnswerAsync(client, firstId, served[index], index == 0 ? "b" : "a");
            answer.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var finish = await FinishAsync(client, firstId);
        finish.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await StartQuizAsync(client, lessonId, 5);

        var unseen = questionIds.Except(served).Single();
        var secondServed = ServedQuestionIds(second);
        secondServed.Should().Equal([unseen, .. served.Skip(1)]);
        secondServed.Should().NotContain(served[0]).And.OnlyHaveUniqueItems();
        second.GetProperty("id").GetGuid().Should().NotBe(firstId);
    }

    [Fact]
    public async Task Post_AfterFinishedQuiz_SmallPoolServesWholePoolOnce()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var first = await StartQuizAsync(client, lessonId, 5);
        var firstId = first.GetProperty("id").GetGuid();
        foreach (var questionId in ServedQuestionIds(first))
        {
            using var answer = await AnswerAsync(client, firstId, questionId, "a");
            answer.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var finish = await FinishAsync(client, firstId);
        finish.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await StartQuizAsync(client, lessonId, 5);

        ServedQuestionIds(second).Should().HaveCount(2).And.OnlyHaveUniqueItems().And.BeEquivalentTo(questionIds);
    }

    private static List<Guid> ServedQuestionIds(JsonElement body)
    {
        return body.GetProperty("items")
            .EnumerateArray()
            .OrderBy(x => x.GetProperty("position").GetInt32())
            .Select(x => x.GetProperty("questionId").GetGuid())
            .ToList();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
