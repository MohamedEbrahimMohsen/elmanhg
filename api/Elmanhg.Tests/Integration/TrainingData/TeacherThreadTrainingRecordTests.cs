using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherThreads;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;
using static Elmanhg.Tests.Integration.TrainingData.TrainingDataTestData;

namespace Elmanhg.Tests.Integration.TrainingData;

public sealed class TeacherThreadTrainingRecordTests(ApiFactory factory)
{
    private const string ReplyText = "The unit is the newton.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reply_FinalReplyAfterFollowUp_WritesClosedRecord()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, ThreadFor(student.Id, subjectId).AnsweredBy(teacher.Id).FollowedUp().Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var record = (await ReadThreadRecordsAsync(factory, thread.Id)).Should().ContainSingle().Subject;
        (record.Trigger, record.Rating, record.StudentHash).Should().Be((TeacherThreadTrainingTrigger.Closed, (int?)null, ExpectedHash(student.Id)));
        var messages = record.ReadMessages();
        messages.Should().HaveCount(4);
        (messages[^1].Author, messages[^1].Text).Should().Be((TeacherThreadTrainingAuthor.Teacher, ReplyText));
        record.Messages.Should().NotContain(teacher.Id.ToString());
    }

    [Fact]
    public async Task Rate_AnsweredThread_WritesClosedRecordWithRating()
    {
        var subjectId = await SeedSubjectAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var (student, client) = await SignedInStudentAsync();
        var thread = await SeedThreadAsync(factory, ThreadFor(student.Id, subjectId).AnsweredBy(teacher.Id).Build());

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 5 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var record = (await ReadThreadRecordsAsync(factory, thread.Id)).Should().ContainSingle().Subject;
        (record.Trigger, record.Rating).Should().Be((TeacherThreadTrainingTrigger.Closed, (int?)5));
    }

    [Fact]
    public async Task Rate_ClosedThread_AppendsRatedAfterCloseRecord()
    {
        var subjectId = await SeedSubjectAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var (student, client) = await SignedInStudentAsync();
        var thread = await SeedThreadAsync(factory, ThreadFor(student.Id, subjectId).AnsweredBy(teacher.Id).FinalReplied().Build());

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 4 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await ReadThreadRecordsAsync(factory, thread.Id);
        records.Select(x => (x.Trigger, x.Rating)).Should().BeEquivalentTo([(TeacherThreadTrainingTrigger.Closed, (int?)null), (TeacherThreadTrainingTrigger.RatedAfterClose, (int?)4)]);
    }

    [Fact]
    public async Task Rate_OpenThread_Returns409AndWritesNoRecord()
    {
        var subjectId = await SeedSubjectAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var (student, client) = await SignedInStudentAsync();
        var thread = await SeedThreadAsync(factory, ThreadFor(student.Id, subjectId).ClaimedBy(teacher.Id).Build());

        using var response = await client.PostAsJsonAsync(RatingRoute(thread.Id), new { rating = 5 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("TEACHER_THREAD_NOT_ANSWERED");
        (await ReadThreadRecordsAsync(factory, thread.Id)).Should().BeEmpty();
    }

    private async Task<(User Student, HttpClient Client)> SignedInStudentAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        return (student, await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken));
    }

    private static TeacherThreadBuilder ThreadFor(Guid studentId, Guid subjectId) => new TeacherThreadBuilder().ForStudent(studentId).WithContext(ContextFor(subjectId)).SubmittedAt(DateTimeOffset.UtcNow.AddDays(-1));

    private Task<Guid> SeedSubjectAsync() => ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);

    private static string RatingRoute(Guid threadId) => $"{TeacherThreadTestData.Route}/{threadId}/rating";
}
