using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.Shared;

public sealed class ServableTypeCountsTests
{
    [Fact]
    public void ToResults_EssayAvailable_ListsServedTypesOnly()
    {
        var available = new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 2, [QuestionType.Essay] = 3 };

        var results = ServableTypeCounts.ToResults(available);

        results.Select(x => x.Type).Should().Equal(ServableQuestionSpecification.ServedTypes);
        results.Single(x => x.Type == QuestionType.Mcq).Count.Should().Be(2);
        results.Should().NotContain(x => x.Type == QuestionType.Essay);
    }
}
