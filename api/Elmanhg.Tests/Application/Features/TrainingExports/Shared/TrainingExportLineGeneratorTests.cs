using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Text.Json;

namespace Elmanhg.Tests.Application.Features.TrainingExports.Shared;

public sealed class TrainingExportLineGeneratorTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private readonly IStudentIdHasher _studentIdHasher = Substitute.For<IStudentIdHasher>();

    [Fact]
    public void Attempt_Record_OmitsAttemptIdAndScrubsAnswer()
    {
        var session = new SessionBuilder().Build(1);
        var attempt = session.RecordAttempt(session.Items[0], """{"text":"رقمي 01012345678"}""", SessionBuilder.Grade(1m), 500);
        var record = AttemptTrainingRecord.From(attempt, session.Kind, new QuestionPlacement(attempt.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), StudentHash, RecordedAt);

        var line = Serialize(TrainingExportLineGenerator.Attempt(record));

        line.Should().NotContain(attempt.Id.ToString()).And.NotContain(session.Id.ToString()).And.NotContain("01012345678").And.Contain("[number]");
        line.Should().Contain($"\"recordId\":\"{record.Id}\"").And.Contain($"\"questionId\":\"{attempt.QuestionId}\"");
    }

    [Fact]
    public void Avatar_Record_UsesScopedConversationKeyAndScrubsTexts()
    {
        var conversation = new AvatarConversationBuilder().WithExchange("ايميلي a@b.co", "زور www.site.org").Build();
        var record = AvatarTrainingRecord.From(conversation, conversation.Messages[0], conversation.Messages[1], StudentHash, RecordedAt);
        _studentIdHasher.HashSourceId(TrainingExportLineGenerator.AvatarConversationScope, conversation.Id).Returns("conversation-key");

        var line = TrainingExportLineGenerator.Avatar(record, _studentIdHasher);

        (line.ConversationKey, line.StudentText, line.AssistantText).Should().Be(("conversation-key", "ايميلي [email]", "زور [url]"));
        Serialize(line).Should().NotContain(conversation.Id.ToString()).And.NotContain(conversation.Messages[0].Id.ToString()).And.NotContain(conversation.Messages[1].Id.ToString());
    }

    [Fact]
    public void TeacherThread_Record_DropsAttemptIdFromContextAndScrubsMessages()
    {
        var (attemptId, teacherId) = (Guid.NewGuid(), Guid.NewGuid());
        var context = new TeacherThreadContext(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", Guid.NewGuid(), 1, "stem", attemptId);
        var thread = new TeacherThreadBuilder().WithContext(context).AnsweredBy(teacherId).Build();
        thread.FollowUp("كلمني على 010 1234 5678", TeacherThreadBuilder.DefaultSubmittedAt.AddHours(2), TimeSpan.FromHours(24));
        thread.Reply(teacherId, "Newtons.", TeacherThreadBuilder.DefaultSubmittedAt.AddHours(3));
        var record = TeacherThreadTrainingRecord.From(thread, TeacherThreadTrainingTrigger.Closed, StudentHash, thread.ClosedAt!.Value, RecordedAt);
        _studentIdHasher.HashSourceId(TrainingExportLineGenerator.TeacherThreadScope, thread.Id).Returns("thread-key");

        var line = TrainingExportLineGenerator.TeacherThread(record, _studentIdHasher);

        line.ThreadKey.Should().Be("thread-key");
        var serialized = Serialize(line);
        serialized.Should().NotContain(attemptId.ToString()).And.NotContain(thread.Id.ToString()).And.NotContain("010 1234 5678").And.Contain("[number]");
        serialized.Should().Contain($"\"questionId\":\"{context.QuestionId}\"");
    }

    [Fact]
    public void EssayGrade_Record_ScrubsAnswerAndJustification()
    {
        var grade = new EssayGradeBuilder().WithAnswer("راسلني على me@x.com").Build();
        var assessment = new EssayAssessment([new EssayCriterionScore("c1", "Definition", 1, 2, "انظر https://x.org")], "رقمه 01112345678", 0.9m, "claude-sonnet-5", "v1", 900, 150, 0.00495m);
        grade.Complete(assessment, new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, EssayGradeBuilder.DefaultRequestedAt.AddSeconds(40));
        var record = EssayGradeTrainingRecord.From(grade, SessionKind.Quiz, new QuestionPlacement(grade.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), StudentHash, RecordedAt);

        var line = TrainingExportLineGenerator.EssayGrade(record);

        line.Justification.Should().Be("رقمه [number]");
        var serialized = Serialize(line);
        serialized.Should().NotContain(grade.Id.ToString()).And.NotContain(grade.SessionId.ToString()).And.NotContain("me@x.com").And.NotContain("https://x.org");
        serialized.Should().Contain("[email]").And.Contain("[url]");
    }

    private static string Serialize<TLine>(TLine line) => JsonSerializer.Serialize(line, TrainingExportJson.SerializerOptions);
}
