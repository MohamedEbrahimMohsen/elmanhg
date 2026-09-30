using Core.Errors;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.GetEssayGradeReview;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.GradeReviews.GetEssayGradeReview;

public sealed class GetEssayGradeReviewHandlerTests
{
    private const string EditedStem = "<p>Explain inertia with an example.</p>";
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly QuestionBuilder _questions = new();
    private readonly List<EssayGrade> _grades = [];
    private readonly Question _question;

    public GetEssayGradeReviewHandlerTests()
    {
        _question = _questions.Essay().Approved().Build();
        _essayGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssayGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<EssayGrade>, IQueryable<EssayGrade>>?>(), Arg.Any<Func<IQueryable<EssayGrade>, IOrderedQueryable<EssayGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<EssayGrade, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _question.Revisions.ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(_ => [_question]);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns([_questions.Lesson]);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns([_questions.Unit]);
    }

    [Fact]
    public async Task Handle_InReview_ReturnsDetailFromServedRevision()
    {
        var grade = Seed(EssayGradeBuilder.InReview(Builder(_question.Version)));
        _question.Update(QuestionType.Essay, QuestionBuilder.EssayContent() with { Stem = EditedStem }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _questions.Lesson, Guid.NewGuid());

        var result = await Handle(grade);

        (result.QuestionVersion, result.QuestionType, result.Stem, result.Kind, result.Status, result.ReviewReason).Should().Be((1, "Essay", QuestionBuilder.EssayContent().Stem, "Essay", "InReview", "LowConfidence"));
        result.GradingSpec.GetProperty("criteria")[0].GetProperty("id").GetString().Should().Be("c1");
        result.Answer.GetProperty("text").GetString().Should().Be(EssayGradeBuilder.DefaultAnswer);
        result.Criteria.Should().Equal(new EssayCriterionResult("c1", "Definition", 1, 2, "ناقص"));
        (result.AiScore, result.Confidence, result.FinalScore, result.Review).Should().Be(((decimal?)2.5m, (decimal?)0.5m, (decimal?)null, (GradeReviewNoteResult?)null));
        (result.UnitName, result.LessonName).Should().Be((_questions.Unit.Name, _questions.Lesson.Name));
        _question.Version.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Reviewed_ReturnsReviewNoteAndFinalScore()
    {
        var grade = Seed(EssayGradeBuilder.InReview(Builder(_question.Version)));
        var reviewedAt = EssayGradeBuilder.DefaultRequestedAt.AddHours(1);
        grade.Override(4m, Guid.NewGuid(), "Full marks for the definition.", reviewedAt);

        var result = await Handle(grade);

        (result.Status, result.FinalScore, result.AiScore).Should().Be(("Graded", (decimal?)4m, (decimal?)2.5m));
        result.Review.Should().Be(new GradeReviewNoteResult("Overridden", "Full marks for the definition.", reviewedAt));
        result.Criteria.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_GradeNeverInReview_ThrowsGradeReviewNotFound()
    {
        var grade = Seed(Builder(_question.Version).Build());
        grade.Complete(EssayGradeBuilder.Assessment(0.9m), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, EssayGradeBuilder.DefaultRequestedAt.AddSeconds(40));

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
    }

    [Fact]
    public async Task Handle_TestModeSession_ThrowsGradeReviewNotFound()
    {
        var session = new SessionBuilder().BuildWithEssay(isTestMode: true);
        SessionRepositoryStub.StubCount(_sessionRepository, session);
        var grade = Seed(EssayGradeBuilder.InReview(Builder(_question.Version).ForSession(session.Id)));

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNotFound);
    }

    [Fact]
    public async Task Handle_RevisionMissing_ThrowsQuestionNotFound()
    {
        var grade = Seed(EssayGradeBuilder.InReview(Builder(9)));

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }

    private EssayGradeBuilder Builder(int version) => new EssayGradeBuilder().ForSubject(_question.SubjectId).ForQuestion(_question.Id, version);

    private EssayGrade Seed(EssayGrade grade)
    {
        _grades.Add(grade);
        return grade;
    }

    private Task<GradeReviewDetailResult> Handle(EssayGrade grade) => new GetEssayGradeReviewHandler(_essayGradeRepository, _sessionRepository, _questionRepository, _lessonRepository, _unitRepository).Handle(new GetEssayGradeReviewQuery(grade.SubjectId, grade.Id), TestContext.Current.CancellationToken);
}
