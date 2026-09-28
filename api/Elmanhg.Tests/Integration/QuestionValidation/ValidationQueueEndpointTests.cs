using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.QuestionValidation.ValidationTestData;

namespace Elmanhg.Tests.Integration.QuestionValidation;

public sealed class ValidationQueueEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_AssignedTeacher_ListsPendingOfAssignedSubjectsOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var physics = await SeedSubjectTreeAsync(factory, "Physics");
        var math = await SeedSubjectTreeAsync(factory, "Math");
        var pendingId = await QuestionTestData.SeedQuestionAsync(factory, physics.LessonId, approved: false, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, physics.LessonId, approved: true, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, math.LessonId, approved: false, cancellationToken);
        var (_, client) = await SeedAssignedTeacherAsync(factory, physics.SubjectId);

        using var response = await client.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(pendingId);
        item.GetProperty("lessonName").GetString().Should().Be("Newton's laws");
        item.GetProperty("unitName").GetString().Should().Be("Mechanics");
        item.GetProperty("openedInSession").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Get_LessonAndDifficultyFilters_ReturnMatchingOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var otherLessonId = await ContentTestData.SeedLessonAsync(factory, tree.UnitId, "Energy", 2, [], cancellationToken);
        var matchingId = await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, otherLessonId, approved: false, cancellationToken);
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var matching = await client.GetAsync($"{Route}?lessonId={tree.LessonId}&difficulty=Medium", cancellationToken);
        using var none = await client.GetAsync($"{Route}?lessonId={tree.LessonId}&difficulty=Hard", cancellationToken);

        (await ReadItemsAsync(matching)).Select(x => x.GetProperty("id").GetGuid()).Should().Equal(matchingId);
        (await ReadItemsAsync(none)).Should().BeEmpty();
    }

    [Fact]
    public async Task Get_MinAgeDays_ExcludesRecentlySubmitted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var newerOldId = await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, cancellationToken);
        var oldestId = await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, cancellationToken);
        await QuestionTestData.SetSubmittedAtAsync(factory, newerOldId, DateTimeOffset.UtcNow.AddDays(-8), cancellationToken);
        await QuestionTestData.SetSubmittedAtAsync(factory, oldestId, DateTimeOffset.UtcNow.AddDays(-10), cancellationToken);
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.GetAsync($"{Route}?minAgeDays=7", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadItemsAsync(response)).Select(x => x.GetProperty("id").GetGuid()).Should().Equal(oldestId, newerOldId);
    }

    [Fact]
    public async Task Get_InvalidPageSize_Returns422()
    {
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.GetAsync($"{Route}?pageSize=1000", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task Get_AsAdmin_Returns403()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFilters_AssignedTeacher_ReturnsOnlyAssignedTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var physics = await SeedSubjectTreeAsync(factory, "Physics");
        await SeedSubjectTreeAsync(factory, "Math");
        var (_, client) = await SeedAssignedTeacherAsync(factory, physics.SubjectId);

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}/filters", cancellationToken);

        body.GetProperty("subjects").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(physics.SubjectId);
        body.GetProperty("units").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(physics.UnitId);
        body.GetProperty("lessons").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(physics.LessonId);
    }

    [Fact]
    public async Task GetQuestion_ResubmittedQuestion_ReturnsRejectionHistory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var (teacher, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);
        var questionId = await QuestionTestData.SeedRejectedQuestionAsync(factory, tree.LessonId, teacher, "Wrong unit", cancellationToken);
        using var admin = await AdminClientAsync(factory);
        using var resubmit = await admin.PutAsJsonAsync($"/api/questions/{questionId}/resubmit", McqRequest("<p>3 + 3 = ?</p>"), cancellationToken);
        resubmit.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}/questions/{questionId}", cancellationToken);

        body.GetProperty("validationStatus").GetString().Should().Be("Pending");
        body.GetProperty("version").GetInt32().Should().Be(2);
        body.GetProperty("revisions").GetArrayLength().Should().Be(2);
        var decision = body.GetProperty("decisions").EnumerateArray().Should().ContainSingle().Subject;
        decision.GetProperty("outcome").GetString().Should().Be("Rejected");
        decision.GetProperty("reason").GetString().Should().Be("Wrong unit");
        decision.GetProperty("decidedByName").GetString().Should().Be("Teacher");
    }

    [Fact]
    public async Task GetQuestion_OtherSubject_Returns403SubjectOutOfScope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var physics = await SeedSubjectTreeAsync(factory, "Physics");
        var math = await SeedSubjectTreeAsync(factory, "Math");
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, math.LessonId, approved: false, cancellationToken);
        var (_, client) = await SeedAssignedTeacherAsync(factory, physics.SubjectId);

        using var response = await client.GetAsync($"{Route}/questions/{questionId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task GetQuestion_Unknown_Returns404QuestionNotFound()
    {
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var (_, client) = await SeedAssignedTeacherAsync(factory, tree.SubjectId);

        using var response = await client.GetAsync($"{Route}/questions/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task PutQuestion_ContentEdit_ResetsSubmittedAt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var testStart = DateTimeOffset.UtcNow;
        var tree = await SeedSubjectTreeAsync(factory, "Physics");
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, tree.LessonId, approved: false, cancellationToken);
        await QuestionTestData.SetSubmittedAtAsync(factory, questionId, DateTimeOffset.UtcNow.AddDays(-10), cancellationToken);
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync($"/api/questions/{questionId}", McqRequest("<p>3 + 3 = ?</p>"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken)).SubmittedAt.Should().BeOnOrAfter(testStart);
    }
}
