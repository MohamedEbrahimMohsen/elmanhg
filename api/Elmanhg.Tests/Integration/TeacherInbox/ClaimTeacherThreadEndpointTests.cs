using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public sealed class ClaimTeacherThreadEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Claim_AssignedTeacher_ClaimsAndPersists()
    {
        var (subjectId, threadId) = await SeedOpenThreadAsync();
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);

        using var response = await client.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("isClaimedByMe").GetBoolean().Should().BeTrue();
        var stored = await ReadThreadAsync(factory, threadId);
        stored.TeacherId.Should().Be(teacher.Id);
        stored.ClaimedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Claim_AlreadyClaimedByAnother_Returns409AlreadyClaimed()
    {
        var (subjectId, threadId) = await SeedOpenThreadAsync();
        var (first, firstClient) = await SignedInTeacherForAsync(factory, subjectId);
        var (_, secondClient) = await SignedInTeacherForAsync(factory, subjectId);
        using var claimed = await firstClient.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken);

        using var response = await secondClient.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_ALREADY_CLAIMED");
        (await ReadThreadAsync(factory, threadId)).TeacherId.Should().Be(first.Id);
    }

    [Fact]
    public async Task Claim_TwoTeachersAtOnce_ExactlyOneWins()
    {
        var (subjectId, threadId) = await SeedOpenThreadAsync();
        var (first, firstClient) = await SignedInTeacherForAsync(factory, subjectId);
        var (second, secondClient) = await SignedInTeacherForAsync(factory, subjectId);

        var responses = await Task.WhenAll(firstClient.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken), secondClient.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken));

        responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var loser = responses.Single(x => x.StatusCode == HttpStatusCode.Conflict);
        (await ReadCodeAsync(loser)).Should().BeOneOf("TEACHER_THREAD_ALREADY_CLAIMED", "TEACHER_THREAD_MODIFIED_CONCURRENTLY");
        var winnerId = responses[0].StatusCode == HttpStatusCode.OK ? first.Id : second.Id;
        (await ReadThreadAsync(factory, threadId)).TeacherId.Should().Be(winnerId);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Claim_TeacherOfOtherSubject_Returns403()
    {
        var (_, threadId) = await SeedOpenThreadAsync();
        var otherSubject = await ScopeTestData.SeedSubjectAsync(factory, $"Math {Guid.NewGuid():N}", CancellationToken);
        var (_, client) = await SignedInTeacherForAsync(factory, otherSubject);

        using var response = await client.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadThreadAsync(factory, threadId)).TeacherId.Should().BeNull();
    }

    [Fact]
    public async Task Claim_Student_Returns403()
    {
        var (_, threadId) = await SeedOpenThreadAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.PostAsync($"{Route}/{threadId}/claim", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid SubjectId, Guid ThreadId)> SeedOpenThreadAsync()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
        return (subjectId, thread.Id);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
