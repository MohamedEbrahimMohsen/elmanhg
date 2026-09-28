using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ExamBlueprints;

public sealed class ExamBlueprintServabilityTests
{
    private readonly ExamBlueprint _blueprint = new ExamBlueprintBuilder().BuildForUnit();

    [Fact]
    public void EnsureServable_EnoughQuestions_DoesNotThrow()
    {
        var act = () => _blueprint.EnsureServable(new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 2 });

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureServable_Short_ThrowsExamShortfallWithTypes()
    {
        var act = () => _blueprint.EnsureServable(new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 1 });

        var exception = act.Should().Throw<BusinessRuleViolationCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.ExamShortfall);
        exception.Context!["types"].Should().Be("Mcq 1/2");
    }
}
