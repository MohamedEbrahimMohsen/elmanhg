using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.GradeMathSteps;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;
using System.Text.Json;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.GradeMathSteps;

public sealed class GradeMathStepsHandlerTests
{
    private const string EditedSpec = """{"acceptedAnswers":["x = 2"],"form":"equivalent","modelSolution":["a","b","c"],"stepsWeight":100}""";
    private static readonly DateTimeOffset Now = MathStepGradeBuilder.DefaultRequestedAt.AddMinutes(1);
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IAiMathStepGradingClient _mathStepGradingClient = Substitute.For<IAiMathStepGradingClient>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly QuestionBuilder _questions = new();
    private readonly List<MathStepGrade> _grades = [];
    private readonly List<Lesson> _lessons = [];
    private Question _question;
    private AiMathStepGradingRequest? _captured;

    public GradeMathStepsHandlerTests()
    {
        _question = _questions.MathStepsGraded().Build();
        _lessons.Add(_questions.Lesson);
        _timeProvider.GetUtcNow().Returns(Now);
        StubRepositories();
        _mathStepGradingClient.GradeAsync(Arg.Do<AiMathStepGradingRequest>(x => _captured = x), Arg.Any<CancellationToken>()).Returns(Reply(0.9m));
    }

    [Fact]
    public async Task Handle_StepGradedDue_GradesAgainstServedRevisionAndSaves()
    {
        var grade = Seed("""{"steps":[" 2x = 4 ","  "],"finalAnswer":" x = 2 "}""");
        _question.Update(QuestionType.MathSteps, QuestionBuilder.MathStepsContent(EditedSpec), new QuestionMetadata(QuestionDifficulty.Medium, null, []), _questions.Lesson, Guid.NewGuid());

        await Handle(grade.Id);

        (_captured!.Question, _captured.FinalAnswer, _captured.AcceptedAnswers.Single()).Should().Be(("Solve 2x + 3 = 7.", "x = 2", "x = 2"));
        _captured.ModelSolution.Should().Equal("2x = 4", "x = 2");
        _captured.Steps.Should().Equal("2x = 4");
        (grade.Status, grade.Score, grade.NormalisedScore, grade.Confidence).Should().Be((MathStepGradeStatus.Graded, (decimal?)1.75m, (decimal?)0.875m, (decimal?)0.9m));
        grade.ReadSteps().Select(x => (x.Step, x.Points)).Should().Equal(("2x = 4", 2), ("x = 2", 1));
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LowConfidence_MarksInReview()
    {
        _mathStepGradingClient.GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>()).Returns(Reply(0.2m));
        var grade = Seed();

        await Handle(grade.Id);

        (grade.Status, grade.ReviewReason).Should().Be((MathStepGradeStatus.InReview, (MathStepReviewReason?)MathStepReviewReason.LowConfidence));
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FinalOnlyQuestion_CompletesWithoutAiCall()
    {
        _question = new QuestionBuilder().MathSteps().Build();
        var grade = Seed();

        await Handle(grade.Id);

        (grade.Status, grade.Model).Should().Be((MathStepGradeStatus.Graded, (string?)null));
        grade.ToQuestionGrade().Should().Be(new QuestionGrade(2m, 1m, GradeOutcome.Correct, GradeFeedback.MathFinalAnswerOnly));
        await _mathStepGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoStudentSteps_CompletesWithZeroStepCredit()
    {
        var grade = Seed("""{"steps":[],"finalAnswer":"x = 2"}""");

        await Handle(grade.Id);

        grade.ToQuestionGrade().Should().Be(new QuestionGrade(1m, 0.5m, GradeOutcome.Partial, GradeFeedback.MathStepTally(0, 2)));
        await _mathStepGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VerdictMissing_DoesNothing()
    {
        var grade = new MathStepGradeBuilder().ForQuestion(_question.Id, 1).WithVerdict(null).Build();
        _grades.Add(grade);

        await Handle(grade.Id);

        grade.Status.Should().Be(MathStepGradeStatus.Pending);
        await ShouldNotGrade();
    }

    [Fact]
    public async Task Handle_NotDue_DoesNothing()
    {
        var grade = Seed();
        grade.FailAttempt("MATH_STEP_GRADING_UNAVAILABLE", Now, 4, TimeSpan.FromSeconds(30));

        await Handle(grade.Id);

        await ShouldNotGrade();
    }

    [Fact]
    public async Task Handle_AiUnavailable_PropagatesWithoutSaving()
    {
        _mathStepGradingClient.GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.MathStepGradingUnavailable));
        var grade = Seed();

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathStepGradingUnavailable);
        grade.Status.Should().Be(MathStepGradeStatus.Pending);
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RevisionMissing_ThrowsQuestionNotFound()
    {
        var grade = new MathStepGradeBuilder().ForQuestion(_question.Id, 9).Build();
        _grades.Add(grade);

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await ShouldNotGrade();
    }

    [Fact]
    public async Task Handle_Request_OmitsStudentIdentity()
    {
        var grade = Seed();

        await Handle(grade.Id);

        var json = JsonSerializer.Serialize(_captured);
        json.Should().NotContain(grade.StudentId.ToString()).And.NotContain(grade.SessionId.ToString()).And.NotContain("Equivalent");
        _captured!.Subject.Should().Be("Physics");
    }

    [Fact]
    public async Task Handle_LessonGone_GradesWithoutContext()
    {
        _lessons.Clear();
        var grade = Seed();

        await Handle(grade.Id);

        (_captured!.Subject, grade.Status).Should().Be(((string?)null, MathStepGradeStatus.Graded));
        _captured.Objectives.Should().BeEmpty();
    }

    private static AiMathStepGradingResult Reply(decimal confidence) => new([new AiMathStepScore(0, 2, "Correct."), new AiMathStepScore(1, 1, "Incomplete.")], 3, 4, "Good working; finish the division.", confidence, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.004m);

    private void StubRepositories()
    {
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
        _lessonRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.FirstOrDefault(call.Arg<Expression<Func<Lesson, bool>>>().Compile()));
        _unitRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Unit }.FirstOrDefault(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()));
        _subjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Subject }.FirstOrDefault(call.Arg<Expression<Func<Subject, bool>>>().Compile()));
        _questionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _question }.FirstOrDefault(call.Arg<Expression<Func<Question, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _question.Revisions.ToList());
    }

    private MathStepGrade Seed(string answer = MathStepGradeBuilder.DefaultAnswer)
    {
        var grade = new MathStepGradeBuilder().ForQuestion(_question.Id, 1).WithAnswer(answer).Build();
        _grades.Add(grade);
        return grade;
    }

    private async Task ShouldNotGrade()
    {
        await _mathStepGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task Handle(Guid gradeId) => new GradeMathStepsHandler(_mathStepGradeRepository, _questionRepository, _lessonRepository, _unitRepository, _subjectRepository, new RichTextExtractor(), _mathStepGradingClient, Options.Create(new MathStepGradingOptions()), _timeProvider).Handle(new GradeMathStepsCommand(gradeId), TestContext.Current.CancellationToken);
}
