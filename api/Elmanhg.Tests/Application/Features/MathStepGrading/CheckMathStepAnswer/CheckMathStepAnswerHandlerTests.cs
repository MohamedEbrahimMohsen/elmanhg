using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.CheckMathStepAnswer;

public sealed class CheckMathStepAnswerHandlerTests
{
    private const string EditedSpec = """{"acceptedAnswers":["x = 9"],"form":"factored"}""";
    private static readonly DateTimeOffset Now = MathStepGradeBuilder.DefaultRequestedAt.AddMinutes(1);
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IAiMathCheckClient _mathCheckClient = Substitute.For<IAiMathCheckClient>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly QuestionBuilder _questions = new();
    private readonly Question _question;
    private readonly List<MathStepGrade> _grades = [];
    private AiMathCheckRequest? _captured;

    public CheckMathStepAnswerHandlerTests()
    {
        _question = _questions.MathStepsGraded().Build();
        _timeProvider.GetUtcNow().Returns(Now);
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _question.Revisions.ToList());
        Reply(MathAnswerVerdict.NotEquivalent);
    }

    [Fact]
    public async Task Handle_DueWithoutVerdict_RecordsVerdictAndSaves()
    {
        var grade = Seed(null);
        _question.Update(QuestionType.MathSteps, QuestionBuilder.MathStepsContent(EditedSpec), new QuestionMetadata(QuestionDifficulty.Medium, null, []), _questions.Lesson, Guid.NewGuid());

        await Handle(grade.Id);

        (_captured!.Answer, _captured.Expected.Single(), _captured.Form).Should().Be(("x = 2", "x = 2", MathAnswerForm.Equivalent));
        (grade.FinalAnswerVerdict, grade.Status).Should().Be(((MathAnswerVerdict?)MathAnswerVerdict.NotEquivalent, MathStepGradeStatus.Pending));
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyChecked_DoesNothing()
    {
        var grade = Seed(MathAnswerVerdict.Equivalent);

        await Handle(grade.Id);

        await ShouldNotCheck();
    }

    [Fact]
    public async Task Handle_NotDue_DoesNothing()
    {
        var grade = Seed(null);
        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", Now, 4, TimeSpan.FromSeconds(30));

        await Handle(grade.Id);

        await ShouldNotCheck();
    }

    [Fact]
    public async Task Handle_Missing_DoesNothing()
    {
        await Handle(Guid.NewGuid());

        await ShouldNotCheck();
    }

    [Fact]
    public async Task Handle_StillUnchecked_ThrowsMathCheckUnavailableWithoutSaving()
    {
        Reply(MathAnswerVerdict.Unchecked);
        var grade = Seed(null);

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathCheckUnavailable);
        grade.FinalAnswerVerdict.Should().BeNull();
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RevisionMissing_ThrowsQuestionNotFound()
    {
        var grade = new MathStepGradeBuilder().ForQuestion(_question.Id, 9).WithVerdict(null).Build();
        _grades.Add(grade);

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await ShouldNotCheck();
    }

    private void Reply(MathAnswerVerdict verdict) => _mathCheckClient.CheckAsync(Arg.Do<AiMathCheckRequest>(x => _captured = x), Arg.Any<CancellationToken>()).Returns(new AiMathCheckResult(verdict, null, []));

    private MathStepGrade Seed(MathAnswerVerdict? verdict)
    {
        var grade = new MathStepGradeBuilder().ForQuestion(_question.Id, 1).WithVerdict(verdict).WithAnswer("""{"steps":["2x = 4"],"finalAnswer":" x = 2 "}""").Build();
        _grades.Add(grade);
        return grade;
    }

    private async Task ShouldNotCheck()
    {
        await _mathCheckClient.DidNotReceive().CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task Handle(Guid gradeId) => new CheckMathStepAnswerHandler(_mathStepGradeRepository, _questionRepository, _mathCheckClient, _timeProvider).Handle(new CheckMathStepAnswerCommand(gradeId), TestContext.Current.CancellationToken);
}
