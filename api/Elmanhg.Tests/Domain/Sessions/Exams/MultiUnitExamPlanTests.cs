using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Sessions.Exams;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Exams;

public sealed class MultiUnitExamPlanTests
{
    private readonly MultiUnitExamPlan _plan = new([Unit(new ExamTypeCount(QuestionType.Mcq, 12)), Unit(new ExamTypeCount(QuestionType.Mcq, 8))], 30, 50);

    [Fact]
    public void EnsureServable_UnionCoversCounts_DoesNotThrow()
    {
        var act = () => _plan.EnsureServable(new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 20 });

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureServable_TypeShort_ThrowsExamShortfallWithTypes()
    {
        var act = () => _plan.EnsureServable(new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 3 });

        var exception = act.Should().Throw<BusinessRuleViolationCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.ExamShortfall);
        exception.Context.Should().ContainKey("types").WhoseValue.Should().Be("Mcq 3/20");
    }

    private static MultiUnitExamUnitPlan Unit(params ExamTypeCount[] counts) => new(Guid.NewGuid(), false, counts, null);
}
