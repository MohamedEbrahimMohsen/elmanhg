using Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Application.Features.Mastery;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.ApplyMathStepGrade;

public sealed class ApplyMathStepGradeHandlerTests
{
    private static readonly DateTimeOffset Now = MathStepGradeBuilder.DefaultRequestedAt.AddMinutes(2);
    private static readonly QuestionGrade StepGrade = new(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2));
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SessionBuilder _builder = new();
    private readonly List<MathStepGrade> _grades = [];

    public ApplyMathStepGradeHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
        QuestionMasteryRepositoryStub.StubFind(_questionMasteryRepository);
    }

    [Fact]
    public async Task Handle_Graded_RecordsAiAttemptAndStartsMastery()
    {
        var (session, grade) = SeedGraded(isTestMode: false);

        await Handle(grade.Id);

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.GradedBy, attempt.Score, attempt.CreatedAt, attempt.TimeTakenMilliseconds).Should().Be((AttemptGrader.AI, 1.5m, grade.RequestedAt, 1500));
        attempt.ReadFeedback().Should().Be(GradeFeedback.MathStepTally(1, 2));
        grade.AppliedAt.Should().Be(Now);
        await _questionMasteryRepository.Received(1).AddAsync(Arg.Is<QuestionMastery>(x => x.StudentId == _builder.StudentId && x.QuestionId == grade.QuestionId && x.LatestAttemptId == attempt.Id), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GradedAfterUncheckedSubmit_RecordsMastery()
    {
        var (session, grade) = Seed(isTestMode: false, verdict: MathAnswerVerdict.Unchecked);
        grade.RecordVerdict(MathAnswerVerdict.Equivalent, Now.AddMinutes(-1));
        grade.Complete(null, new QuestionGrade(2m, 1m, GradeOutcome.Correct, GradeFeedback.MathFinalAnswerOnly), 0.7m, Now.AddMinutes(-1));

        await Handle(grade.Id);

        session.Attempts.Should().ContainSingle().Which.Outcome.Should().Be(GradeOutcome.Correct);
        await _questionMasteryRepository.Received(1).AddAsync(Arg.Is<QuestionMastery>(x => x.QuestionId == grade.QuestionId), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Graded_TestModeSession_RecordsAttemptWithoutMastery()
    {
        var (session, grade) = SeedGraded(isTestMode: true);

        await Handle(grade.Id);

        session.Attempts.Should().ContainSingle().Which.GradedBy.Should().Be(AttemptGrader.AI);
        await _questionMasteryRepository.DidNotReceive().AddAsync(Arg.Any<QuestionMastery>(), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InReview_RecordsNoAttempt()
    {
        var (session, grade) = Seed(isTestMode: false);
        grade.Complete(MathStepGradeBuilder.Assessment(0.3m), StepGrade, 0.7m, Now);

        await Handle(grade.Id);

        session.Attempts.Should().BeEmpty();
        grade.AppliedAt.Should().BeNull();
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SessionMissing_CompletesWithoutAttempt()
    {
        var (session, grade) = SeedGraded(isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository);

        await Handle(grade.Id);

        (grade.Status, grade.AppliedAt).Should().Be((MathStepGradeStatus.Graded, (DateTimeOffset?)Now));
        session.Attempts.Should().BeEmpty();
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyApplied_DoesNothing()
    {
        var (session, grade) = SeedGraded(isTestMode: false);
        grade.MarkApplied(Now.AddSeconds(-5));

        await Handle(grade.Id);

        session.Attempts.Should().BeEmpty();
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private (Session Session, MathStepGrade Grade) SeedGraded(bool isTestMode)
    {
        var (session, grade) = Seed(isTestMode);
        grade.Complete(MathStepGradeBuilder.Assessment(), StepGrade, 0.7m, Now.AddMinutes(-1));
        return (session, grade);
    }

    private (Session Session, MathStepGrade Grade) Seed(bool isTestMode, MathAnswerVerdict verdict = MathAnswerVerdict.Equivalent)
    {
        var session = _builder.Build(isTestMode: isTestMode);
        var item = session.Items[1];
        session.SubmitForAiGrading(item, MathStepGradeBuilder.DefaultAnswer, 0);
        var grade = new MathStepGradeBuilder().ForStudent(_builder.StudentId).ForSession(session.Id).ForQuestion(item.QuestionId, item.QuestionVersion).WithVerdict(verdict).WithTimeTaken(1500).Build();
        _grades.Add(grade);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        return (session, grade);
    }

    private Task Handle(Guid gradeId) => new ApplyMathStepGradeHandler(_mathStepGradeRepository, _sessionRepository, _questionMasteryRepository, Options.Create(new MasteryOptions()), _timeProvider).Handle(new ApplyMathStepGradeCommand(gradeId), TestContext.Current.CancellationToken);
}
