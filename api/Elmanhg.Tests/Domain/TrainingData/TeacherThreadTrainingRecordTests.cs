using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingData;

public sealed class TeacherThreadTrainingRecordTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void From_FinalRepliedThread_StoresAllMessagesInOrderAsText()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FinalReplied().Build();

        var record = TeacherThreadTrainingRecord.From(thread, TeacherThreadTrainingTrigger.Closed, StudentHash, thread.ClosedAt!.Value, RecordedAt);

        var messages = record.ReadMessages();
        messages.Select(x => x.Author).Should().Equal(TeacherThreadTrainingAuthor.Student, TeacherThreadTrainingAuthor.Teacher, TeacherThreadTrainingAuthor.Student, TeacherThreadTrainingAuthor.Teacher);
        messages.Select(x => x.Text).Should().Equal("Why is F = ma?", "Because force equals mass times acceleration.", "Can you show the units?", "Newtons.");
        messages.Select(x => x.SentAt).Should().BeInAscendingOrder();
        (record.StudentHash, record.ThreadId, record.Trigger, record.OccurredAt, record.RecordedAt).Should().Be((StudentHash, thread.Id, TeacherThreadTrainingTrigger.Closed, thread.ClosedAt.Value, RecordedAt));
    }

    [Fact]
    public void From_VoiceAnsweredThread_StoresTranscriptWithoutAudioUrl()
    {
        const string audioUrl = "/api/media/voice/0123456789abcdef0123456789abcdef.webm";
        var thread = new TeacherThreadBuilder().AnsweredByVoice(_teacherId, audioUrl).Build();
        thread.Rate(4, TeacherThreadBuilder.DefaultSubmittedAt.AddHours(5));

        var record = TeacherThreadTrainingRecord.From(thread, TeacherThreadTrainingTrigger.Closed, StudentHash, thread.ClosedAt!.Value, RecordedAt);

        var teacherEntry = record.ReadMessages().Single(x => x.Author == TeacherThreadTrainingAuthor.Teacher);
        (teacherEntry.Kind, teacherEntry.Text).Should().Be((TeacherMessageKind.Voice, "Voice transcript."));
        record.Messages.Should().NotContain(audioUrl);
    }

    [Fact]
    public void From_ThreadWithImage_FlagsImageWithoutStoringUrl()
    {
        const string imageUrl = "/api/media/images/0123456789abcdef0123456789abcdef.png";
        var thread = new TeacherThreadBuilder().WithImage(imageUrl).AnsweredBy(_teacherId).Rated(5).Build();

        var record = TeacherThreadTrainingRecord.From(thread, TeacherThreadTrainingTrigger.Closed, StudentHash, thread.ClosedAt!.Value, RecordedAt);

        record.ReadMessages().Select(x => x.HasImage).Should().Equal(true, false);
        record.Messages.Should().NotContain(imageUrl);
    }

    [Fact]
    public void From_ClosedThread_CopiesContextIdsRatingAndOmitsTeacherId()
    {
        var context = new TeacherThreadContext(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", Guid.NewGuid(), 3, "Why?", Guid.NewGuid());
        var thread = new TeacherThreadBuilder().WithContext(context).AnsweredBy(_teacherId).Rated(3).Build();

        var record = TeacherThreadTrainingRecord.From(thread, TeacherThreadTrainingTrigger.Closed, StudentHash, thread.ClosedAt!.Value, RecordedAt);

        (record.SubjectId, record.UnitId, record.LessonId).Should().Be((context.SubjectId, context.UnitId, context.LessonId));
        (record.QuestionId, record.QuestionVersion, record.AttemptId).Should().Be((context.QuestionId, context.QuestionVersion, context.AttemptId));
        (record.Rating, record.SubmittedAt).Should().Be(((int?)3, thread.SubmittedAt));
        record.Context.Should().Be(thread.Context);
        record.Messages.Should().NotContain(_teacherId.ToString());
    }

    [Fact]
    public void From_OpenThread_ThrowsInvalidOperationException()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var act = () => TeacherThreadTrainingRecord.From(thread, TeacherThreadTrainingTrigger.Closed, StudentHash, RecordedAt, RecordedAt);

        act.Should().Throw<InvalidOperationException>().WithMessage("Only a closed thread becomes a training record.");
    }
}
