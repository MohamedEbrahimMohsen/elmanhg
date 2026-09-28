using Elmanhg.Application.Exams.AutoSubmitExam;
using Elmanhg.Application.Shared.Options;
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
        _handler = new AutoSubmitExamHandler(_sessionRepository, _questionRepository, _questionMasteryRepository, Options.Create(new ExamsOptions()), Options.Create(new MasteryOptions()), _timeProvider);
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
}
