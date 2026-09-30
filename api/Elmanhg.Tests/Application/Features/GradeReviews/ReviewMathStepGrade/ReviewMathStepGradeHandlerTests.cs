using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.ReviewMathStepGrade;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
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

namespace Elmanhg.Tests.Application.Features.GradeReviews.ReviewMathStepGrade;

public sealed class ReviewMathStepGradeHandlerTests
{
    private static readonly DateTimeOffset Now = MathStepGradeBuilder.DefaultRequestedAt.AddHours(2);
    private static readonly QuestionGrade StepGrade = new(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2));
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IGradeReviewNotifier _notifier = Substitute.For<IGradeReviewNotifier>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<MathStepGrade> _grades = [];
    private readonly List<Question> _questions = [];

    public ReviewMathStepGradeHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => _questions.Where(x => call.Arg<IReadOnlyCollection<Guid>>().Contains(x.Id)).SelectMany(x => x.Revisions).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _questions.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns([]);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns([]);
        QuestionMasteryRepositoryStub.StubFind(_questionMasteryRepository);
    }

    [Fact]
    public async Task Handle_OverrideUncheckedGrade_RecordsTeacherAttemptMasteryAndNotifies()
    {
        var (session, grade) = SeedUnchecked();

        var result = await Handle(grade, GradeReviewDecision.Overridden, 2m, "Correct method.");

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.GradedBy, attempt.Score, attempt.NormalisedScore).Should().Be((AttemptGrader.Teacher, 2m, 1m));
        attempt.ReadFeedback().Should().BeNull();
        await _questionMasteryRepository.Received(1).AddAsync(Arg.Is<QuestionMastery>(x => x.QuestionId == grade.QuestionId && x.LatestAttemptId == attempt.Id), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyReviewedAsync(_builder.StudentId, session.Id, grade.QuestionId, Arg.Any<CancellationToken>());
        (result.Kind, result.FinalScore, result.Review!.Decision).Should().Be(("MathSteps", (decimal?)2m, "Overridden"));
    }

    [Fact]
    public async Task Handle_AcceptLowConfidence_RecordsAttemptWithStoredFeedback()
    {
        var (session, grade) = Seed();
        grade.Complete(MathStepGradeBuilder.Assessment(0.4m), StepGrade, 0.7m, Now.AddMinutes(-30));

        await Handle(grade, GradeReviewDecision.Accepted, null, null);

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.GradedBy, attempt.Score).Should().Be((AttemptGrader.Teacher, 1.5m));
        attempt.ReadFeedback().Should().Be(GradeFeedback.MathStepTally(1, 2));
    }

    [Fact]
    public async Task Handle_GradeInOtherSubject_ThrowsGradeReviewNotFound()
    {
        var (_, grade) = SeedUnchecked();

        var act = () => CreateHandler().Handle(new ReviewMathStepGradeCommand(Guid.NewGuid(), grade.Id, GradeReviewDecision.Overridden, 2m, "Correct method."), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestModeSession_ThrowsGradeReviewNotFound()
    {
        var (session, grade) = Seed(verdict: null, isTestMode: true);
        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", Now.AddMinutes(-30), 1, TimeSpan.FromSeconds(30));

        var act = () => Handle(grade, GradeReviewDecision.Overridden, 2m, "Correct method.");

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
        session.Attempts.Should().BeEmpty();
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotInReview_ThrowsGradeNotInReview()
    {
        var (_, grade) = Seed();

        var act = () => Handle(grade, GradeReviewDecision.Overridden, 2m, "Correct method.");

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.GradeNotInReview);
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var (_, grade) = SeedUnchecked();

        var act = () => Handle(grade, GradeReviewDecision.Overridden, 2m, "Correct method.");

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private (Session Session, MathStepGrade Grade) SeedUnchecked()
    {
        var (session, grade) = Seed(verdict: null);
        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", Now.AddMinutes(-30), 1, TimeSpan.FromSeconds(30));
        return (session, grade);
    }

    private (Session Session, MathStepGrade Grade) Seed(MathAnswerVerdict? verdict = MathAnswerVerdict.Equivalent, bool isTestMode = false)
    {
        var mcq = _builder.Questions.Approved().Build();
        var math = _builder.Questions.MathSteps().Approved().Build();
        _questions.AddRange([mcq, math]);
        var session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [mcq, math], isTestMode);
        var item = session.Items[1];
        session.SubmitForAiGrading(item, MathStepGradeBuilder.DefaultAnswer, 0);
        var grade = new MathStepGradeBuilder().ForStudent(_builder.StudentId).ForSession(session.Id).ForQuestion(item.QuestionId, item.QuestionVersion).WithVerdict(verdict).Build();
        _grades.Add(grade);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        SessionRepositoryStub.StubCount(_sessionRepository, session);
        return (session, grade);
    }

    private ReviewMathStepGradeHandler CreateHandler() => new(_mathStepGradeRepository, _sessionRepository, _questionMasteryRepository, _questionRepository, _lessonRepository, _unitRepository, Options.Create(new MasteryOptions()), _notifier, _timeProvider, _currentUserService);

    private Task<GradeReviewDetailResult> Handle(MathStepGrade grade, GradeReviewDecision decision, decimal? score, string? comment) => CreateHandler().Handle(new ReviewMathStepGradeCommand(grade.SubjectId, grade.Id, decision, score, comment), TestContext.Current.CancellationToken);
}
