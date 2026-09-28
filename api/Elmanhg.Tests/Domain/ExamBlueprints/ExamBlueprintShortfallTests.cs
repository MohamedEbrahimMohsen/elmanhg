using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ExamBlueprints;

public sealed class ExamBlueprintShortfallTests
{
    [Fact]
    public void Find_EnoughForEveryType_ReturnsEmpty()
    {
        var available = new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 5, [QuestionType.Fill] = 2 };

        var shortfalls = ExamBlueprintShortfall.Find([new ExamTypeCount(QuestionType.Mcq, 5), new ExamTypeCount(QuestionType.Fill, 1)], available);

        shortfalls.Should().BeEmpty();
    }

    [Fact]
    public void Find_TypeShort_ReturnsRequiredAvailableMissing()
    {
        var available = new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 3 };

        var shortfalls = ExamBlueprintShortfall.Find([new ExamTypeCount(QuestionType.Mcq, 5)], available);

        shortfalls.Should().Equal(new ExamTypeShortfall(QuestionType.Mcq, 5, 3));
        shortfalls[0].Missing.Should().Be(2);
    }

    [Fact]
    public void Find_TypeAbsentFromAvailable_CountsAsZero()
    {
        var shortfalls = ExamBlueprintShortfall.Find([new ExamTypeCount(QuestionType.Short, 1)], new Dictionary<QuestionType, int>());

        shortfalls.Should().Equal(new ExamTypeShortfall(QuestionType.Short, 1, 0));
    }

    [Fact]
    public void Find_ZeroRequirement_IsIgnored()
    {
        var shortfalls = ExamBlueprintShortfall.Find([new ExamTypeCount(QuestionType.Short, 0)], new Dictionary<QuestionType, int>());

        shortfalls.Should().BeEmpty();
    }

    [Fact]
    public void Find_SeveralShort_OrderedByType()
    {
        var shortfalls = ExamBlueprintShortfall.Find([new ExamTypeCount(QuestionType.Fill, 2), new ExamTypeCount(QuestionType.Mcq, 2)], new Dictionary<QuestionType, int>());

        shortfalls.Select(x => x.Type).Should().Equal(QuestionType.Mcq, QuestionType.Fill);
    }
}
