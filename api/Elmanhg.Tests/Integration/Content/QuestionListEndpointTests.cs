using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.Content;

public sealed class QuestionListEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions";

    [Fact]
    public async Task Get_ByLesson_ReturnsLessonQuestionsWithLessonName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?lessonId={lessonId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("totalItems").GetInt32().Should().Be(2);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("lessonName").GetString()).Should().AllBe("Newton's laws");
        items.Select(x => x.GetProperty("type").GetString()).Should().AllBe("Mcq");
    }

    [Fact]
    public async Task Get_RejectedWithReasonFilter_ReturnsTeacherNameAndReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        var rejectedId = await QuestionTestData.SeedRejectedQuestionAsync(factory, lessonId, teacher, "Wrong unit conversion", cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?lessonId={lessonId}&status=Rejected&rejectionReason=UNIT", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(rejectedId);
        item.GetProperty("validationStatus").GetString().Should().Be("Rejected");
        item.GetProperty("teacherName").GetString().Should().Be("Teacher");
        item.GetProperty("rejectionReason").GetString().Should().Be("Wrong unit conversion");
    }

    [Fact]
    public async Task Get_ByTeacher_ReturnsQuestionsThatTeacherDecided()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var first = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var second = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var firstQuestionId = await QuestionTestData.SeedRejectedQuestionAsync(factory, lessonId, first, "Wrong unit", cancellationToken);
        await QuestionTestData.SeedRejectedQuestionAsync(factory, lessonId, second, "Unclear", cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?lessonId={lessonId}&teacherId={first.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(firstQuestionId);
        item.GetProperty("validatedBy").GetGuid().Should().Be(first.Id);
    }

    [Fact]
    public async Task Get_MinVersion_ReturnsOnlyEditedQuestions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        var editedId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);
        using var admin = await AdminClientAsync();
        using var edit = await admin.PutAsJsonAsync($"{Route}/{editedId}", McqRequest(lessonId, "<p>3 + 3 = ?</p>"), cancellationToken);
        edit.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await admin.GetAsync($"{Route}?lessonId={lessonId}&minVersion=2", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(editedId);
        item.GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Get_ByType_ReturnsOnlyThatType()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var mcq = await admin.PostAsJsonAsync(Route, McqRequest(lessonId), cancellationToken);
        using var trueFalse = await admin.PostAsJsonAsync(Route, new { lessonId, type = "TrueFalse", stem = "<p>Mass is a vector.</p>", body = Json("{}"), gradingSpec = Json("""{"correctAnswer":false}"""), difficulty = "Easy", maxScore = 1 }, cancellationToken);
        (mcq.StatusCode, trueFalse.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));

        using var response = await admin.GetAsync($"{Route}?lessonId={lessonId}&type=TrueFalse", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadItemsAsync(response)).Should().ContainSingle().Which.GetProperty("type").GetString().Should().Be("TrueFalse");
    }

    [Fact]
    public async Task Get_PageSizeOverCap_Returns422QuestionPageSizeInvalid()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}?pageSize=101", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var (subjectId, _) = await SeedLessonAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<List<JsonElement>> ReadItemsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static object McqRequest(Guid lessonId, string stem = "<p>2 + 2 = ?</p>")
    {
        return new { lessonId, type = "Mcq", stem, body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), explanation = "<p>Add the numbers.</p>", difficulty = "Medium", maxScore = 1, tags = new[] { "arithmetic" } };
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law"], TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> TeacherClientAsync(Guid assignedSubjectId)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, assignedSubjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
