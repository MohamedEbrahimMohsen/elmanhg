using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.Shared;

public sealed class EssayAssessmentsTests
{
    private static readonly AiEssayGradingRequest Request = new("Q", [Criterion("c1", "Definition", 2), Criterion("c2", "Example", 3)], ["Model"], "Essay", null, []);
    private static readonly AiEssayGradingResult Result = new([new AiEssayCriterionScore("c2", 1, " Weak example. "), new AiEssayCriterionScore("c1", 2, "Correct.")], 3, 5, " Good. ", 0.61237m, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.00495m);

    [Fact]
    public void From_OrdersCriteriaByRubricWithTitlesAndMaxPoints()
    {
        var assessment = EssayAssessments.From(Request, Result);

        assessment.Criteria.Should().Equal(new EssayCriterionScore("c1", "Definition", 2, 2, "Correct."), new EssayCriterionScore("c2", "Example", 1, 3, "Weak example."));
        (assessment.Justification, assessment.Confidence, assessment.Model, assessment.PromptVersion).Should().Be(("Good.", 0.6124m, "claude-sonnet-5", "v1"));
        (assessment.InputTokens, assessment.OutputTokens, assessment.CostUsd).Should().Be((900, 150, 0.00495m));
    }

    [Fact]
    public void Awards_MapsCriterionPoints()
    {
        EssayAssessments.Awards(Result).Should().Equal(new EssayCriterionAward("c2", 1), new EssayCriterionAward("c1", 2));
    }

    private static AiRubricCriterion Criterion(string id, string title, int points) => new(id, title, null, points, [new AiRubricLevel(0, "None"), new AiRubricLevel(points, "Full")]);
}
