using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionEssayTests
{
    private const string Essay = """{"text":"القصور الذاتي هو ممانعة الجسم لتغيير حالته."}""";
    private const int ReportedMilliseconds = 1_000_000;
    private static readonly QuestionGrade FullEssay = new(5m, 1m, GradeOutcome.Correct, null);
    private static readonly DateTimeOffset AnsweredAt = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset GradedAt = AnsweredAt.AddMinutes(1);
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void SubmitEssay_NewEssay_SavesPendingAnswerAndMeasuresTime()
    {
        var session = _builder.BuildWithEssay();
        var item = session.Items[1];

        var submission = session.SubmitEssay(item, Essay, ReportedMilliseconds);

        submission.IsNew.Should().BeTrue();
        item.SavedAnswer.Should().Be(Essay);
        (item.AnswerSavedAt, session.LastActivityAt).Should().Be((submission.SubmittedAt, submission.SubmittedAt));
        submission.TimeTakenMilliseconds.Should().BeInRange(0, ReportedMilliseconds);
        session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void SubmitEssay_SameEssayAgain_ReturnsReplay()
    {
        var session = _builder.BuildWithEssay();
        var item = session.Items[1];
        session.SubmitEssay(item, Essay, 0);
        var lastActivity = session.LastActivityAt;

        var replay = session.SubmitEssay(item, Essay, 0);

        replay.IsNew.Should().BeFalse();
        (item.SavedAnswer, session.LastActivityAt).Should().Be((Essay, lastActivity));
    }

    [Fact]
    public void SubmitEssay_DifferentEssayAfterSubmit_ThrowsSessionQuestionAlreadyAnswered()
    {
        var session = _builder.BuildWithEssay();
        session.SubmitEssay(session.Items[1], Essay, 0);

        var act = () => session.SubmitEssay(session.Items[1], """{"text":"إجابة أخرى"}""", 0);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionAlreadyAnswered);
    }

    [Fact]
    public void SubmitEssay_FinishedQuiz_ThrowsSessionAlreadySubmitted()
    {
        var session = _builder.BuildWithEssay();
        session.Submit();

        var act = () => session.SubmitEssay(session.Items[1], Essay, 0);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionAlreadySubmitted);
    }

    [Fact]
    public void SubmitEssay_Exam_ThrowsInvalidOperation()
    {
        var session = new ExamSessionBuilder().BuildWithEssay();

        var act = () => session.SubmitEssay(session.Items[0], Essay, 0);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CurrentPosition_PendingEssayAtFirstOpenPosition_SkipsIt()
    {
        var answeredFirst = _builder.BuildWithEssay();
        answeredFirst.RecordAttempt(answeredFirst.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        answeredFirst.SubmitEssay(answeredFirst.Items[1], Essay, 0);
        var mcqOpen = new SessionBuilder().BuildWithEssay();
        mcqOpen.SubmitEssay(mcqOpen.Items[1], Essay, 0);

        (answeredFirst.CurrentPosition, mcqOpen.CurrentPosition).Should().Be(((int?)null, (int?)1));
    }

    [Fact]
    public void FindPendingEssayAnswer_ExamSavedAnswer_ReturnsNull()
    {
        var session = new ExamSessionBuilder().BuildWithEssay();
        var essay = session.Items.Single(x => x.MaxScore == 5);
        session.SaveExamAnswer(essay, Essay, TimeSpan.FromSeconds(30), ExamSessionBuilder.Now.AddMinutes(1));

        session.FindPendingEssayAnswer(essay).Should().BeNull();
    }

    [Fact]
    public void RecordAttempt_ItemWithPendingEssay_ThrowsSessionQuestionAlreadyAnswered()
    {
        var session = _builder.BuildWithEssay();
        session.SubmitEssay(session.Items[1], Essay, 0);

        var act = () => session.RecordAttempt(session.Items[1], """{"text":""}""", SessionBuilder.Grade(0m), 0);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionAlreadyAnswered);
        session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void RecordEssayAttempt_OpenQuiz_AddsAiAttemptAtAnsweredTime()
    {
        var session = _builder.BuildWithEssay();
        var item = session.Items[1];
        session.SubmitEssay(item, Essay, 0);
        var lastActivity = session.LastActivityAt;

        var attempt = session.RecordEssayAttempt(item, Essay, FullEssay, AttemptGrader.AI, 1234, AnsweredAt.AddTicks(5), GradedAt);

        (attempt!.GradedBy, attempt.CreatedAt, attempt.Score, attempt.NormalisedScore, attempt.TimeTakenMilliseconds).Should().Be((AttemptGrader.AI, AnsweredAt, 5m, 1m, 1234));
        (session.LastActivityAt, session.UpdationDate, session.ScorePercent).Should().Be((lastActivity, GradedAt, (decimal?)null));
        session.FindPendingEssayAnswer(item).Should().BeNull();
    }

    [Fact]
    public void RecordEssayAttempt_FinishedQuiz_RecomputesScorePercent()
    {
        var session = _builder.BuildWithEssay();
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        session.SubmitEssay(session.Items[1], Essay, 0);
        session.Submit();
        var before = session.ScorePercent;

        session.RecordEssayAttempt(session.Items[1], Essay, FullEssay, AttemptGrader.AI, 0, AnsweredAt, GradedAt);

        (before, session.ScorePercent).Should().Be(((decimal?)16.67m, (decimal?)100m));
    }

    [Fact]
    public void RecordEssayAttempt_AlreadyRecorded_ReturnsNull()
    {
        var session = _builder.BuildWithEssay();
        session.SubmitEssay(session.Items[1], Essay, 0);
        session.RecordEssayAttempt(session.Items[1], Essay, FullEssay, AttemptGrader.AI, 0, AnsweredAt, GradedAt);

        var again = session.RecordEssayAttempt(session.Items[1], Essay, FullEssay, AttemptGrader.AI, 0, AnsweredAt, GradedAt);

        again.Should().BeNull();
        session.Attempts.Should().ContainSingle();
    }

    [Fact]
    public void SubmitExam_WrittenEssayDeferred_CreatesAttemptsForOthersOnly()
    {
        var session = new ExamSessionBuilder().BuildWithEssay();
        var mcq = session.Items.Single(x => x.MaxScore == 1);
        var essay = session.Items.Single(x => x.MaxScore == 5);
        var savedAt = ExamSessionBuilder.Now.AddMinutes(1);
        session.SaveExamAnswer(mcq, SessionBuilder.AnswerB, TimeSpan.FromSeconds(30), savedAt);
        session.SaveExamAnswer(essay, Essay, TimeSpan.FromSeconds(30), savedAt);

        var attempts = session.SubmitExam(new Dictionary<Guid, QuestionGrade> { [mcq.QuestionId] = SessionBuilder.Grade(1m) }, new HashSet<Guid> { essay.QuestionId }, savedAt.AddMinutes(1));

        attempts.Should().ContainSingle().Which.QuestionId.Should().Be(mcq.QuestionId);
        session.ScorePercent.Should().Be(16.67m);
    }
}
