using Elmanhg.Application.Exams.AutoSubmitExam;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.AutoSubmitExam;

public sealed class AutoSubmitExamHandlerTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly Session _session;
    private readonly AutoSubmitExamHandler _handler;

    public AutoSubmitExamHandlerTests()
    {
        var questions = _builder.BuildQuestions(2);
        _session = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(2), questions, [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
        _session.SaveExamAnswer(_session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(questions.SelectMany(x => x.Revisions).ToList());
        _questionMasteryRepository.FindAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>()).Returns(new List<QuestionMastery>());
        _handler = new AutoSubmitExamHandler(_sessionRepository, _questionRepository, _questionMasteryRepository, _essayGradeRepository, Options.Create(new ExamsOptions()), Options.Create(new MasteryOptions()), _timeProvider);
    }

    [Fact]
    public async Task Handle_ExpiredExam_SubmitsAndSaves()
    {
        _timeProvider.GetUtcNow().Returns(_session.Deadline.GetValueOrDefault().AddSeconds(31));

        await _handler.Handle(new AutoSubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        _session.SubmittedAt.Should().NotBeNull();
        _session.ScorePercent.Should().Be(50.00m);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithinGrace_LeavesOpen()
    {
        _timeProvider.GetUtcNow().Returns(_session.Deadline.GetValueOrDefault().AddSeconds(10));

        await _handler.Handle(new AutoSubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        _session.IsSubmitted.Should().BeFalse();
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSession_DoesNothing()
    {
        _timeProvider.GetUtcNow().Returns(_session.Deadline.GetValueOrDefault().AddHours(1));

        await _handler.Handle(new AutoSubmitExamCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _session.IsSubmitted.Should().BeFalse();
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrittenEssay_RequestsGrade()
    {
        var exam = new ExamSessionBuilder();
        var mcq = exam.Questions.Approved().Build();
        var essay = exam.Questions.Essay().Approved().Build();
        var blueprint = ExamBlueprint.CreateForUnit(exam.Questions.Unit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1), new ExamTypeCount(QuestionType.Essay, 1)], null, 30, 50), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());
        var session = Session.StartUnitExam(exam.StudentId, exam.Questions.Unit, blueprint, [mcq, essay], [exam.Questions.Lesson], false, ExamSessionBuilder.Now);
        session.SaveExamAnswer(session.GetItem(essay.Id)!, """{"text":"القصور الذاتي"}""", Grace, ExamSessionBuilder.Now.AddMinutes(1));
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([.. mcq.Revisions, .. essay.Revisions]);
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>()).Returns([essay]);
        _timeProvider.GetUtcNow().Returns(session.Deadline.GetValueOrDefault().AddSeconds(31));

        await _handler.Handle(new AutoSubmitExamCommand(session.Id), TestContext.Current.CancellationToken);

        await _essayGradeRepository.Received(1).AddRangeAsync(Arg.Is<List<EssayGrade>>(list => list.Count == 1 && list[0].SessionId == session.Id && list[0].QuestionId == essay.Id), Arg.Any<CancellationToken>());
        session.IsSubmitted.Should().BeTrue();
    }
}
