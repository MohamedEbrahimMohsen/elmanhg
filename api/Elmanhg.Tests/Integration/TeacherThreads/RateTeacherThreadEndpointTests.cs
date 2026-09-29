using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class RateTeacherThreadEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rate_AnsweredThread_ClosesAndStoresRating()
    {
        var subjectId = await SeedSubjectAsync();
        var (teacher, teacherClient) = await SignedInTeacherForAsync(factory, subjectId);
        var (student, client) = await SignedInStudentAsync();
        var thread = await SeedThreadAsync(factory, Answered(student.Id, subjectId, teacher.Id).Build());

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 5 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("rating").GetInt32(), body.GetProperty("status").GetString()).Should().Be((5, "Closed"));
        var stored = await ReadThreadAsync(factory, thread.Id);
        (stored.Rating, stored.Status).Should().Be(((int?)5, TeacherThreadStatus.Closed));
        stored.ClosedAt.Should().NotBeNull();
        var inboxThread = await teacherClient.GetFromJsonAsync<JsonElement>($"{Route}/{thread.Id}", CancellationToken);
        inboxThread.GetProperty("rating").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Rate_AlreadyRated_Returns409()
    {
        var subjectId = await SeedSubjectAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var (student, client) = await SignedInStudentAsync();
        var thread = await SeedThreadAsync(factory, Answered(student.Id, subjectId, teacher.Id).Rated(3).Build());

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 5 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_ALREADY_RATED");
        (await ReadThreadAsync(factory, thread.Id)).Rating.Should().Be(3);
    }

    [Fact]
    public async Task Rate_OutOfRange_Returns422()
    {
        var subjectId = await SeedSubjectAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var (student, client) = await SignedInStudentAsync();
        var thread = await SeedThreadAsync(factory, Answered(student.Id, subjectId, teacher.Id).Build());

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 6 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_RATING_INVALID");
        (await ReadThreadAsync(factory, thread.Id)).Rating.Should().BeNull();
    }

    [Fact]
    public async Task Rate_OtherStudentsThread_Returns404()
    {
        var subjectId = await SeedSubjectAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var owner = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, Answered(owner.Id, subjectId, teacher.Id).Build());
        var (_, client) = await SignedInStudentAsync();

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 5 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_NOT_FOUND");
    }

    [Fact]
    public async Task Rate_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        var (teacher, teacherClient) = await SignedInTeacherForAsync(factory, subjectId);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, Answered(student.Id, subjectId, teacher.Id).Build());

        using var response = await teacherClient.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 5 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(User Student, HttpClient Client)> SignedInStudentAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        return (student, await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken));
    }

    private static TeacherThreadBuilder Answered(Guid studentId, Guid subjectId, Guid teacherId) => new TeacherThreadBuilder().ForStudent(studentId).WithContext(ContextFor(subjectId)).AnsweredBy(teacherId);

    private Task<Guid> SeedSubjectAsync() => ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);

    private static string RatingRoute(Guid threadId) => $"{TeacherThreadTestData.Route}/{threadId}/rating";

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
