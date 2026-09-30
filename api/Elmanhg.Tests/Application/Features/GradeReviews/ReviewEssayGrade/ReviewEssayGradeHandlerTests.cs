using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.ReviewEssayGrade;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Mastery;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.GradeReviews.ReviewEssayGrade;

public sealed class ReviewEssayGradeHandlerTests
{
    private static readonly DateTimeOffset Now = EssayGradeBuilder.DefaultRequestedAt.AddHours(2);
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IGradeReviewNotifier _notifier = Substitute.For<IGradeReviewNotifier>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly SessionBuilder _builder = new();
    private readonly List<EssayGrade> _grades = [];
    private readonly List<Question> _questions = [];

    public ReviewEssayGradeHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _currentUserService.UserId.Returns(_teacherId);
        _essayGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssayGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<EssayGrade>, IQueryable<EssayGrade>>?>(), Arg.Any<Func<IQueryable<EssayGrade>, IOrderedQueryable<EssayGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<EssayGrade, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => _questions.Where(x => call.Arg<IReadOnlyCollection<Guid>>().Contains(x.Id)).SelectMany(x => x.Revisions).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _questions.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => new List<Lesson> { _builder.Questions.Lesson }.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => new List<CurriculumUnit> { _builder.Questions.Unit }.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        QuestionMasteryRepositoryStub.StubFind(_questionMasteryRepository);
    }

    [Fact]
    public async Task Handle_Accept_RecordsTeacherAttemptStartsMasteryAndNotifies()
    {
        var (session, grade) = Seed(isTestMode: false);

        var result = await Handle(grade, GradeReviewDecision.Accepted, null, null);

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.GradedBy, attempt.Score, attempt.CreatedAt).Should().Be((AttemptGrader.Teacher, 2.5m, grade.RequestedAt));
        grade.AppliedAt.Should().Be(Now);
        await _questionMasteryRepository.Received(1).AddAsync(Arg.Is<QuestionMastery>(x => x.QuestionId == grade.QuestionId && x.LatestAttemptId == attempt.Id), Arg.Any<CancellationToken>());
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyReviewedAsync(_builder.StudentId, session.Id, grade.QuestionId, Arg.Any<CancellationToken>());
        (result.Review!.Decision, result.FinalScore, result.UnitName, result.LessonName).Should().Be(("Accepted", (decimal?)2.5m, _builder.Questions.Unit.Name, _builder.Questions.Lesson.Name));
    }

    [Fact]
    public async Task Handle_Override_RecordsAttemptWithTeacherScore()
    {
        var (session, grade) = Seed(isTestMode: false);

        await Handle(grade, GradeReviewDecision.Overridden, 4m, "Full marks for the definition.");

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.GradedBy, attempt.Score, attempt.NormalisedScore).Should().Be((AttemptGrader.Teacher, 4m, 0.8m));
    }

    [Fact]
    public async Task Handle_OverrideOnFinishedSession_RecomputesScorePercent()
    {
        var (session, grade) = Seed(isTestMode: false);
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        session.Submit();

        await Handle(grade, GradeReviewDecision.Overridden, 4m, "Full marks for the definition.");

        session.ScorePercent.Should().Be(83.33m);
    }

    [Fact]
    public async Task Handle_TestModeSession_ThrowsGradeReviewNotFound()
    {
        var (session, grade) = Seed(isTestMode: true);

        var act = () => Handle(grade, GradeReviewDecision.Accepted, null, null);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
        (grade.Status, session.Attempts.Count).Should().Be((EssayGradeStatus.InReview, 0));
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyReviewedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SessionMissing_MarksAppliedWithoutAttempt()
    {
        var (session, grade) = Seed(isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository);

        await Handle(grade, GradeReviewDecision.Accepted, null, null);

        (grade.Status, grade.AppliedAt).Should().Be((EssayGradeStatus.Graded, (DateTimeOffset?)Now));
        session.Attempts.Should().BeEmpty();
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GradeInOtherSubject_ThrowsGradeReviewNotFound()
    {
        var (_, grade) = Seed(isTestMode: false);

        var act = () => CreateHandler().Handle(new ReviewEssayGradeCommand(Guid.NewGuid(), grade.Id, GradeReviewDecision.Accepted, null, null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyReviewedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotInReview_ThrowsGradeNotInReview()
    {
        var (_, grade) = Seed(isTestMode: false);
        grade.Accept(Guid.NewGuid(), null, Now.AddMinutes(-5));

        var act = () => Handle(grade, GradeReviewDecision.Overridden, 4m, "Second decision.");

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.GradeNotInReview);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AcceptWithoutAiScore_ThrowsGradeReviewNoAiScore()
    {
        var (_, grade) = Seed(isTestMode: false, gradingFailed: true);

        var act = () => Handle(grade, GradeReviewDecision.Accepted, null, null);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.GradeReviewNoAiScore);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var (_, grade) = Seed(isTestMode: false);

        var act = () => Handle(grade, GradeReviewDecision.Accepted, null, null);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private (Session Session, EssayGrade Grade) Seed(bool isTestMode, bool gradingFailed = false)
    {
        var mcq = _builder.Questions.Approved().Build();
        var essay = _builder.Questions.Essay().Approved().Build();
        _questions.AddRange([mcq, essay]);
        var session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [mcq, essay], isTestMode);
        var item = session.Items[1];
        session.SubmitEssay(item, """{"text":"القصور الذاتي هو ممانعة الجسم لتغيير حالته."}""", 0);
        var builder = new EssayGradeBuilder().ForStudent(_builder.StudentId).ForSession(session.Id).ForSubject(essay.SubjectId).ForQuestion(item.QuestionId, item.QuestionVersion);
        var grade = gradingFailed ? EssayGradeBuilder.GradingFailed(builder) : EssayGradeBuilder.InReview(builder);
        _grades.Add(grade);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        SessionRepositoryStub.StubCount(_sessionRepository, session);
        return (session, grade);
    }

    private ReviewEssayGradeHandler CreateHandler() => new(_essayGradeRepository, _sessionRepository, _questionMasteryRepository, _questionRepository, _lessonRepository, _unitRepository, Options.Create(new MasteryOptions()), _notifier, _timeProvider, _currentUserService);

    private Task<GradeReviewDetailResult> Handle(EssayGrade grade, GradeReviewDecision decision, decimal? score, string? comment) => CreateHandler().Handle(new ReviewEssayGradeCommand(grade.SubjectId, grade.Id, decision, score, comment), TestContext.Current.CancellationToken);
}
