using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Infrastructure.RichText;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.Shared;

public sealed class MathStepGradingRequestFactoryTests
{
    private static readonly MathStepsGradingSpec Spec = new([" x = 2 ", "2"], MathAnswerForm.Equivalent, null, null, ["  2x = 4 ", "x = 2  "], 50);
    private readonly RichTextExtractor _extractor = new();

    [Fact]
    public void Create_ExtractsPlainStemAndTrimsSolutionStepsAndAnswers()
    {
        var request = MathStepGradingRequestFactory.Create("<p>Solve <em>2x + 3 = 7</em>.</p>", Spec, new MathStepsAnswer([" 2x = 4 "], "  x = 2 "), new EssayGradingContext("Math", ["Solve linear equations"]), _extractor, 20000);

        (request.Question, request.FinalAnswer, request.Subject).Should().Be(("Solve 2x + 3 = 7.", "x = 2", "Math"));
        request.ModelSolution.Should().Equal("2x = 4", "x = 2");
        request.AcceptedAnswers.Should().Equal("x = 2", "2");
        request.Steps.Should().Equal("2x = 4");
        request.Objectives.Should().Equal("Solve linear equations");
    }

    [Fact]
    public void Create_DropsBlankStudentSteps()
    {
        var request = MathStepGradingRequestFactory.Create("<p>Q</p>", Spec, new MathStepsAnswer(["  ", null, "x = 2", ""], "x = 2"), null, _extractor, 20000);

        request.Steps.Should().Equal("x = 2");
    }

    [Fact]
    public void Create_TruncatesStemAndObjectivesToFieldMax()
    {
        var request = MathStepGradingRequestFactory.Create("<p>Question text</p>", Spec, new MathStepsAnswer([], "x = 2"), new EssayGradingContext("Math", ["Objective one"]), _extractor, 5);

        (request.Question, request.Objectives[0]).Should().Be(("Quest", "Objec"));
    }

    [Fact]
    public void Create_NoContext_LeavesSubjectNullAndObjectivesEmpty()
    {
        var request = MathStepGradingRequestFactory.Create("<p>Q</p>", Spec, new MathStepsAnswer([], "x = 2"), null, _extractor, 20000);

        request.Subject.Should().BeNull();
        request.Objectives.Should().BeEmpty();
    }
}
