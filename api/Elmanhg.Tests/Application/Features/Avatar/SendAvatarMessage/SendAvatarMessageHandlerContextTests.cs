using Core.Errors;
using Elmanhg.Application.Avatar.SendAvatarMessage;
using Elmanhg.Application.ContentRetrieval.SearchLessonContent;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Avatar.SendAvatarMessage;

public sealed class SendAvatarMessageHandlerContextTests
{
    private readonly AvatarTestData.SendHarness _harness = new();

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _harness.SendAsync(_harness.LessonCommand() with { LessonId = Guid.NewGuid() });

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_DraftLesson_ThrowsLessonNotFound()
    {
        var draft = Lesson.Create(_harness.Builder.Questions.Unit, "Draft", 2, Guid.NewGuid());
        _harness.Lessons.GetWithObjectivesAsync(draft.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(draft);

        var act = () => _harness.SendAsync(_harness.LessonCommand() with { LessonId = draft.Id });

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task Handle_FreeStudentLockedLesson_ThrowsLessonLocked()
    {
        LockLesson();

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonLocked);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotIndexed_SendsPlainExplanationAndSummary()
    {
        _harness.Lesson.Update(_harness.Lesson.Name, "<h2>قانون أوم</h2><p>V = I R</p>", "<p>R = V / I</p>", null, [], Guid.NewGuid());

        await _harness.SendAsync(_harness.LessonCommand());

        _harness.LastChat!.Context.Lesson!.Explanation.Should().Be("قانون أوم\nV = I R");
        _harness.LastChat.Context.Lesson.Summary.Should().Be("R = V / I");
        _harness.LastChat.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LessonTextOverFieldMax_IsTruncated()
    {
        _harness.AvatarOptions.ContextFieldMaxLength = 500;
        _harness.Lesson.Update(_harness.Lesson.Name, $"<p>{new string('a', 600)}</p>", string.Empty, null, [], Guid.NewGuid());

        await _harness.SendAsync(_harness.LessonCommand());

        _harness.LastChat!.Context.Lesson!.Explanation.Should().HaveLength(500);
    }

    [Fact]
    public async Task Handle_QuizSessionNotFound_ThrowsSessionNotFound()
    {
        _harness.Quiz(answered: true);

        var act = () => _harness.SendAsync(new SendAvatarMessageCommand(AvatarEntryPoint.QuizQuestion, null, Guid.NewGuid(), Guid.NewGuid(), null, "لماذا؟"));

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_QuizItemMissing_ThrowsSessionQuestionNotFound()
    {
        var quiz = _harness.Quiz(answered: true);

        var act = () => _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, Guid.NewGuid()));

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotFound);
    }

    [Fact]
    public async Task Handle_QuizQuestionUnanswered_ThrowsAvatarQuestionNotAnswered()
    {
        var quiz = _harness.Quiz(answered: false);

        var act = () => _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, quiz.Items[0].QuestionId));

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarQuestionNotAnswered);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuizQuestionAnswered_SendsQuestionContextAndStemQuery()
    {
        var quiz = _harness.Quiz(answered: true);
        var questionId = quiz.Items[0].QuestionId;

        await _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, questionId));

        var question = _harness.LastChat!.Context.Question!;
        _harness.LastChat.Context.EntryPoint.Should().Be(AiChatEntryPoint.QuizQuestion);
        question.Should().Be(new AiQuestionContext(questionId, "2 + 2 = ?", "3", "4", "Add the numbers."));
        await _harness.Sender.Received(1).Send(Arg.Is<SearchLessonContentQuery>(x => x.LessonId == _harness.Lesson.Id && x.Query == "2 + 2 = ?\nلماذا إجابتي خطأ؟" && x.IncludeQuestionExplanations), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExamReviewUnansweredItem_SendsNullStudentAnswer()
    {
        var exam = Exam(submitted: true);

        await _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.ExamReview, exam, exam.Items[0].QuestionId));

        _harness.LastChat!.Context.EntryPoint.Should().Be(AiChatEntryPoint.ExamReview);
        _harness.LastChat.Context.Question!.StudentAnswer.Should().BeNull();
        _harness.LastChat.Context.Question.CorrectAnswer.Should().Be("4");
    }

    [Fact]
    public async Task Handle_ExamReviewOpenSession_ThrowsSessionNotFound()
    {
        var exam = Exam(submitted: false);

        var act = () => _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.ExamReview, exam, exam.Items[0].QuestionId));

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_UnitMissing_ThrowsLessonNotFound()
    {
        _harness.Units.GetByIdAsync(_harness.Lesson.UnitId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns((CurriculumUnit?)null);

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectMissing_ThrowsLessonNotFound()
    {
        _harness.Subjects.GetByIdAsync(_harness.Builder.Questions.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns((Subject?)null);

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuizQuestionOnLockedLesson_ThrowsLessonLocked()
    {
        var quiz = _harness.Quiz(answered: true);
        LockLesson();

        var act = () => _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, quiz.Items[0].QuestionId));

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonLocked);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExamReviewOnLockedLesson_Passes()
    {
        var exam = Exam(submitted: true);
        LockLesson();

        var result = await _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.ExamReview, exam, exam.Items[0].QuestionId));

        result.Reply.Should().Be("رد");
        _harness.LastChat!.Context.EntryPoint.Should().Be(AiChatEntryPoint.ExamReview);
    }

    [Fact]
    public async Task Handle_ServedRevisionMissing_ThrowsQuestionNotFound()
    {
        var quiz = _harness.Quiz(answered: true);
        _harness.Questions.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);

        var act = () => _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, quiz.Items[0].QuestionId));

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionMissing_ThrowsQuestionNotFound()
    {
        var quiz = _harness.Quiz(answered: true);
        _harness.Questions.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns((Question?)null);

        var act = () => _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, quiz.Items[0].QuestionId));

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task Handle_GlobalEntry_SendsSubjectNamesWithoutRetrieval()
    {
        List<Subject> subjects = [Subject.Create("الأحياء", 2, Guid.NewGuid()), Subject.Create("الفيزياء", 1, Guid.NewGuid())];
        _harness.Subjects.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => call.ArgAt<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(2)!(subjects.AsQueryable()).ToList());

        await _harness.SendAsync(new SendAvatarMessageCommand(AvatarEntryPoint.Global, null, null, null, null, "كيف أذاكر؟"));

        _harness.LastChat!.Context.EntryPoint.Should().Be(AiChatEntryPoint.Global);
        _harness.LastChat.Context.Subjects.Should().Equal("الفيزياء", "الأحياء");
        _harness.LastChat.Context.Lesson.Should().BeNull();
        _harness.LastChat.Sources.Should().BeEmpty();
        await _harness.Sender.DidNotReceive().Send(Arg.Any<SearchLessonContentQuery>(), Arg.Any<CancellationToken>());
    }

    private void LockLesson()
    {
        var lesson = _harness.Lesson;
        _harness.Lessons.GetPublishedSiblingPositionsAsync(lesson.Id, Arg.Any<CancellationToken>()).Returns([new LessonPosition(Guid.NewGuid(), lesson.UnitId, 0, lesson.CreationDate), LessonPosition.Of(lesson)]);
    }

    private Session Exam(bool submitted)
    {
        var questions = _harness.RegisterQuestions(2);
        var exam = Session.StartUnitExam(_harness.Builder.StudentId, _harness.Builder.Questions.Unit, _harness.Builder.Blueprint(2), questions, [_harness.Lesson], false, ExamSessionBuilder.Now);
        if (submitted)
        {
            exam.SubmitExam(new Dictionary<Guid, QuestionGrade>(), ExamSessionBuilder.Now.AddMinutes(1));
        }

        AvatarTestData.StubSessions(_harness.Sessions, exam);
        return exam;
    }
}
