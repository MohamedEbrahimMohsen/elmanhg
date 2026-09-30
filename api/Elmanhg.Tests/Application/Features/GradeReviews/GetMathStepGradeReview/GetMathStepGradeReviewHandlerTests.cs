using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.GetMathStepGradeReview;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.GradeReviews.GetMathStepGradeReview;

public sealed class GetMathStepGradeReviewHandlerTests
{
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly List<MathStepGrade> _grades = [];
    private readonly Question _question = new QuestionBuilder().MathSteps().Approved().Build();

    public GetMathStepGradeReviewHandlerTests()
    {
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _question.Revisions.ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(_ => [_question]);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns([]);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns([]);
    }

    [Fact]
    public async Task Handle_UncheckedGrade_ReturnsAnswerWithoutAiFields()
    {
        var grade = SeedUnchecked();

        var result = await Handle(grade.SubjectId, grade);

        (result.Kind, result.QuestionType, result.ReviewReason, result.AiScore, result.Confidence, result.FinalAnswerVerdict, result.Justification).Should().Be(("MathSteps", "MathSteps", "FinalAnswerUnchecked", (decimal?)null, (decimal?)null, (string?)null, (string?)null));
        result.Steps.Should().BeEmpty();
        result.Criteria.Should().BeEmpty();
        result.Answer.GetProperty("finalAnswer").GetString().Should().Be("x = 2");
        result.GradingSpec.GetProperty("acceptedAnswers")[0].GetString().Should().Be("x = 2");
    }

    [Fact]
    public async Task Handle_GradeInOtherSubject_ThrowsGradeReviewNotFound()
    {
        var grade = SeedUnchecked();

        var act = () => Handle(Guid.NewGuid(), grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
    }

    [Fact]
    public async Task Handle_TestModeSession_ThrowsGradeReviewNotFound()
    {
        var session = new SessionBuilder().Build(isTestMode: true);
        SessionRepositoryStub.StubCount(_sessionRepository, session);
        var grade = SeedUnchecked(session.Id);

        var act = () => Handle(grade.SubjectId, grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
    }

    private MathStepGrade SeedUnchecked(Guid? sessionId = null)
    {
        var grade = new MathStepGradeBuilder().ForSession(sessionId ?? Guid.NewGuid()).ForQuestion(_question.Id, _question.Version).WithVerdict(null).Build();
        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", MathStepGradeBuilder.DefaultRequestedAt.AddSeconds(5), 1, TimeSpan.FromSeconds(30));
        _grades.Add(grade);
        return grade;
    }

    private Task<GradeReviewDetailResult> Handle(Guid subjectId, MathStepGrade grade) => new GetMathStepGradeReviewHandler(_mathStepGradeRepository, _sessionRepository, _questionRepository, _lessonRepository, _unitRepository).Handle(new GetMathStepGradeReviewQuery(subjectId, grade.Id), TestContext.Current.CancellationToken);
}
