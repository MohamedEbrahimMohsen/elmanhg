using Core.DDD.Models;
using Elmanhg.Application.GradeReviews.GetGradeReviewQueue;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.GradeReviews.GetGradeReviewQueue;

public sealed class GetGradeReviewQueueHandlerTests
{
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly QuestionBuilder _questions = new();
    private readonly List<Lesson> _lessons = [];
    private readonly Guid _subjectId = Guid.NewGuid();

    public GetGradeReviewQueueHandlerTests()
    {
        _lessons.Add(_questions.Lesson);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => new List<CurriculumUnit> { _questions.Unit }.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
    }

    [Fact]
    public async Task Handle_Essays_ReturnsItemsWithPlacementAndAiScore()
    {
        var question = _questions.Essay().Build();
        StubQuestions(question);
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder().ForSubject(_subjectId).ForQuestion(question.Id, question.Version), 0.55m);
        _essayGradeRepository.GetInReviewPageAsync(_subjectId, 2, 1, Arg.Any<CancellationToken>()).Returns(new PageData<EssayGrade> { Items = [grade], PageNumber = 2, PageSize = 1, TotalItems = 3, TotalPages = 3 });

        var result = await Handle(GradeReviewKind.Essay, 2, 1);

        result.Items.Should().Equal(new GradeReviewItemResult(grade.Id, "Essay", question.Id, question.Stem, _questions.Unit.Name, _questions.Lesson.Name, "LowConfidence", 5, 2.5m, 0.55m, grade.RequestedAt));
        (result.PageNumber, result.PageSize, result.TotalItems, result.TotalPages).Should().Be((2L, 1L, 3L, 3L));
    }

    [Fact]
    public async Task Handle_MathSteps_ReturnsItemsWithReason()
    {
        var question = _questions.MathSteps().Build();
        StubQuestions(question);
        var grade = new MathStepGradeBuilder().ForQuestion(question.Id, question.Version).WithVerdict(null).Build();
        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", MathStepGradeBuilder.DefaultRequestedAt.AddSeconds(5), 1, TimeSpan.FromSeconds(30));
        _mathStepGradeRepository.GetInReviewPageAsync(_subjectId, 1, 20, Arg.Any<CancellationToken>()).Returns(new PageData<MathStepGrade> { Items = [grade], PageNumber = 1, PageSize = 20, TotalItems = 1, TotalPages = 1 });

        var result = await Handle(GradeReviewKind.MathSteps, 1, 20);

        var item = result.Items.Should().ContainSingle().Subject;
        (item.Kind, item.ReviewReason, item.AiScore, item.Confidence, item.MaxScore).Should().Be(("MathSteps", "FinalAnswerUnchecked", (decimal?)null, (decimal?)null, 2));
        await _essayGradeRepository.DidNotReceive().GetInReviewPageAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MissingLesson_ReturnsEmptyNames()
    {
        _lessons.Clear();
        var question = _questions.Essay().Build();
        StubQuestions(question);
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder().ForSubject(_subjectId).ForQuestion(question.Id, question.Version));
        _essayGradeRepository.GetInReviewPageAsync(_subjectId, 1, 20, Arg.Any<CancellationToken>()).Returns(new PageData<EssayGrade> { Items = [grade], PageNumber = 1, PageSize = 20, TotalItems = 1, TotalPages = 1 });

        var result = await Handle(GradeReviewKind.Essay, 1, 20);

        var item = result.Items.Should().ContainSingle().Subject;
        (item.UnitName, item.LessonName, item.Stem).Should().Be((string.Empty, string.Empty, question.Stem));
    }

    private void StubQuestions(params Question[] questions)
    {
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => questions.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
    }

    private Task<PageData<GradeReviewItemResult>> Handle(GradeReviewKind kind, int pageNumber, int pageSize) => new GetGradeReviewQueueHandler(_essayGradeRepository, _mathStepGradeRepository, _questionRepository, _lessonRepository, _unitRepository).Handle(new GetGradeReviewQueueQuery(_subjectId, kind, pageNumber, pageSize), TestContext.Current.CancellationToken);
}
