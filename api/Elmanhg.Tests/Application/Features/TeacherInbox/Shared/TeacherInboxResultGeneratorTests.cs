using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.Shared;

public sealed class TeacherInboxResultGeneratorTests
{
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _otherTeacherId = Guid.NewGuid();
    private readonly Dictionary<Guid, string> _names;

    public TeacherInboxResultGeneratorTests()
    {
        _names = new Dictionary<Guid, string> { [_studentId] = "Ahmed", [_callerId] = "Mohamed", [_otherTeacherId] = "Sara" };
    }

    [Fact]
    public void GenerateItem_ClaimedByCaller_MapsNamesClaimAndOverdue()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).ClaimedBy(_callerId).Build();

        var item = TeacherInboxResultGenerator.GenerateItem(thread, _names, _callerId, thread.SlaDueAt);

        (item.Id, item.SubjectName, item.LessonName, item.QuestionText).Should().Be((thread.Id, "Physics", "Newton's laws", "Why is F = ma?"));
        (item.StudentName, item.TeacherName, item.IsClaimedByMe, item.IsOverdue, item.Status).Should().Be(("Ahmed", "Mohamed", true, true, TeacherThreadStatus.Open));
    }

    [Fact]
    public void GenerateThread_Unclaimed_CanClaimOnly()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).Build();

        var result = TeacherInboxResultGenerator.GenerateThread(thread, _names, _callerId, thread.SubmittedAt);

        (result.CanClaim, result.CanReply, result.TeacherName, result.IsClaimedByMe, result.ClaimedAt).Should().Be((true, false, (string?)null, false, (DateTimeOffset?)null));
        (result.StudentName, result.Context.LessonName).Should().Be(("Ahmed", "Newton's laws"));
        result.Messages.Should().ContainSingle().Which.IsFromStudent.Should().BeTrue();
    }

    [Fact]
    public void GenerateThread_ClaimedByCallerOpen_CanReplyOnly()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).ClaimedBy(_callerId).Build();

        var result = TeacherInboxResultGenerator.GenerateThread(thread, _names, _callerId, thread.SubmittedAt);

        (result.CanClaim, result.CanReply, result.IsClaimedByMe, result.TeacherName, result.ClaimedAt).Should().Be((false, true, true, "Mohamed", thread.ClaimedAt));
    }

    [Fact]
    public void GenerateThread_ClaimedByAnother_NeitherClaimNorReply()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).ClaimedBy(_otherTeacherId).Build();

        var result = TeacherInboxResultGenerator.GenerateThread(thread, _names, _callerId, thread.SubmittedAt);

        (result.CanClaim, result.CanReply, result.IsClaimedByMe, result.TeacherName).Should().Be((false, false, false, "Sara"));
    }

    [Fact]
    public void GenerateThread_RatedThread_ReturnsRating()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(_callerId).Rated(4).Build();

        var result = TeacherInboxResultGenerator.GenerateThread(thread, _names, _callerId, thread.SubmittedAt);

        result.Rating.Should().Be(4);
    }

    [Fact]
    public void GenerateReminder_UnclaimedOverdueThread_MapsFields()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).Build();

        var result = TeacherInboxResultGenerator.GenerateReminder(thread, TeacherThreadSlaEventKind.SecondReminder, _callerId, thread.SlaDueAt.AddMinutes(1));

        (result.ThreadId, result.SubjectName, result.LessonName, result.QuestionText).Should().Be((thread.Id, "Physics", "Newton's laws", "Why is F = ma?"));
        (result.Kind, result.IsClaimedByMe, result.IsOverdue, result.SlaDueAt).Should().Be((TeacherThreadSlaEventKind.SecondReminder, false, true, thread.SlaDueAt));
    }
}
