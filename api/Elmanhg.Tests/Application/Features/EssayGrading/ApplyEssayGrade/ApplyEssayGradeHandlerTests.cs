using Elmanhg.Application.EssayGrading.ApplyEssayGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Application.Features.Mastery;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.ApplyEssayGrade;

public sealed class ApplyEssayGradeHandlerTests
{
    private static readonly DateTimeOffset Now = EssayGradeBuilder.DefaultRequestedAt.AddMinutes(2);
    private static readonly QuestionGrade FullGrade = new(5m, 1m, GradeOutcome.Correct, null);
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SessionBuilder _builder = new();
    private readonly List<EssayGrade> _grades = [];

    public ApplyEssayGradeHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _essayGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssayGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<EssayGrade>, IQueryable<EssayGrade>>?>(), Arg.Any<Func<IQueryable<EssayGrade>, IOrderedQueryable<EssayGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<EssayGrade, bool>>>().Compile()));
        QuestionMasteryRepositoryStub.StubFind(_questionMasteryRepository);
    }

    [Fact]
    public async Task Handle_Graded_RecordsAiAttemptAndStartsMastery()
    {
        var (session, grade) = SeedGraded(isTestMode: false);

        await Handle(grade.Id);

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.GradedBy, attempt.Score, attempt.CreatedAt, attempt.TimeTakenMilliseconds).Should().Be((AttemptGrader.AI, 5m, grade.RequestedAt, 1500));
        grade.AppliedAt.Should().Be(Now);
        await _questionMasteryRepository.Received(1).AddAsync(Arg.Is<QuestionMastery>(x => x.StudentId == _builder.StudentId && x.QuestionId == grade.QuestionId && x.LatestAttemptId == attempt.Id), Arg.Any<CancellationToken>());
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Graded_TestModeSession_RecordsAttemptWithoutMastery()
    {
        var (session, grade) = SeedGraded(isTestMode: true);

        await Handle(grade.Id);

        session.Attempts.Should().ContainSingle().Which.GradedBy.Should().Be(AttemptGrader.AI);
        await _questionMasteryRepository.DidNotReceive().AddAsync(Arg.Any<QuestionMastery>(), Arg.Any<CancellationToken>());
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LowConfidence_RecordsNoAttempt()
    {
        var (session, grade) = Seed(isTestMode: false);
        grade.Complete(EssayGradeBuilder.Assessment(0.3m), FullGrade, 0.7m, Now);

        await Handle(grade.Id);

        session.Attempts.Should().BeEmpty();
        grade.AppliedAt.Should().BeNull();
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SessionMissing_CompletesWithoutAttempt()
    {
        var (session, grade) = SeedGraded(isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository);

        await Handle(grade.Id);

        (grade.Status, grade.AppliedAt).Should().Be((EssayGradeStatus.Graded, (DateTimeOffset?)Now));
        session.Attempts.Should().BeEmpty();
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyApplied_DoesNothing()
    {
        var (session, grade) = SeedGraded(isTestMode: false);
        grade.MarkApplied(Now.AddSeconds(-5));

        await Handle(grade.Id);

        session.Attempts.Should().BeEmpty();
        await _sessionRepository.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Expression<Func<Session, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>());
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private (Session Session, EssayGrade Grade) SeedGraded(bool isTestMode)
    {
        var (session, grade) = Seed(isTestMode);
        grade.Complete(EssayGradeBuilder.Assessment(), FullGrade, 0.7m, Now.AddMinutes(-1));
        return (session, grade);
    }

    private (Session Session, EssayGrade Grade) Seed(bool isTestMode)
    {
        var session = _builder.BuildWithEssay(isTestMode);
        var item = session.Items[1];
        session.SubmitEssay(item, """{"text":"القصور الذاتي هو ممانعة الجسم لتغيير حالته."}""", 0);
        var grade = new EssayGradeBuilder().ForStudent(_builder.StudentId).ForSession(session.Id).ForQuestion(item.QuestionId, item.QuestionVersion).WithTimeTaken(1500).Build();
        _grades.Add(grade);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        return (session, grade);
    }

    private Task Handle(Guid gradeId) => new ApplyEssayGradeHandler(_essayGradeRepository, _sessionRepository, _questionMasteryRepository, Options.Create(new MasteryOptions()), _timeProvider).Handle(new ApplyEssayGradeCommand(gradeId), TestContext.Current.CancellationToken);
}
