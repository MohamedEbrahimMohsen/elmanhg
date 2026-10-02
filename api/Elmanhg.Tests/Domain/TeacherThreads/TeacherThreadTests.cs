using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly TeacherThreadContext _context = new(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);

    [Fact]
    public void Submit_ValidInput_OpensThreadWithSlaAndFirstStudentMessage()
    {
        var thread = TeacherThread.Submit(_studentId, _context, "  Why is F = ma?  ", "/api/media/teacher-threads/a.png", SubmittedAt, TeacherThreadSlaPolicies.WallClock());

        (thread.Status, thread.StudentId, thread.SubjectId, thread.CreatedBy).Should().Be((TeacherThreadStatus.Open, _studentId, _context.SubjectId, (Guid?)_studentId));
        (thread.SubmittedAt, thread.SlaDueAt).Should().Be((SubmittedAt, SubmittedAt.AddHours(24)));
        (thread.SlaWindowStartedAt, thread.FirstReminderDueAt, thread.SecondReminderDueAt).Should().Be((thread.SubmittedAt, SubmittedAt.AddHours(12), SubmittedAt.AddHours(20)));
        var message = thread.Messages.Should().ContainSingle().Subject;
        (message.ThreadId, message.SenderId, message.Kind, message.Text).Should().Be((thread.Id, _studentId, TeacherMessageKind.Text, "Why is F = ma?"));
        (message.ImageUrl, message.CreatedAt).Should().Be(("/api/media/teacher-threads/a.png", SubmittedAt));
    }

    [Fact]
    public void Submit_BlankText_ThrowsTeacherMessageTextRequired()
    {
        var act = () => TeacherThread.Submit(_studentId, _context, "   ", null, SubmittedAt, TeacherThreadSlaPolicies.WallClock());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherMessageTextRequired);
    }

    [Fact]
    public void Submit_SubMicrosecondTime_TruncatesToMicroseconds()
    {
        var thread = TeacherThread.Submit(_studentId, _context, "Why?", null, SubmittedAt.AddTicks(7), TeacherThreadSlaPolicies.WallClock());

        (thread.SubmittedAt.Ticks % 10).Should().Be(0);
        (thread.SubmittedAt, thread.SlaDueAt).Should().Be((SubmittedAt, SubmittedAt.AddHours(24)));
    }

    [Fact]
    public void ReadContext_AfterSubmit_ReturnsTheSubmittedContext()
    {
        var context = new TeacherThreadContext(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", Guid.NewGuid(), 3, "<p>Why?</p>", Guid.NewGuid());

        var thread = TeacherThread.Submit(_studentId, context, "Why?", null, SubmittedAt, TeacherThreadSlaPolicies.WallClock());

        thread.ReadContext().Should().Be(context);
    }

    [Fact]
    public void IsOverdueAt_BeforeSlaDue_ReturnsFalse()
    {
        var thread = new TeacherThreadBuilder().Build();

        thread.IsOverdueAt(thread.SlaDueAt.AddSeconds(-1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3600)]
    public void IsOverdueAt_AtOrAfterSlaDue_ReturnsTrue(int secondsAfterDue)
    {
        var thread = new TeacherThreadBuilder().Build();

        thread.IsOverdueAt(thread.SlaDueAt.AddSeconds(secondsAfterDue)).Should().BeTrue();
    }
}
